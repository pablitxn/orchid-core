using Application.Interfaces;
using Aspose.Cells;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Loads Excel workbooks using Aspose.Cells and applies header detection heuristics.
/// </summary>
public sealed class CellsWorkbookLoader(
    IFileStorageService storage,
    ILogger<CellsWorkbookLoader> logger) : IWorkbookLoader
{
    private readonly IFileStorageService _storage = storage;
    private readonly ILogger<CellsWorkbookLoader> _logger = logger;

    public async Task<WorkbookEntity> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Stream stream;
        try
        {
            // First try to get the file from storage service using just the filename
            stream = await _storage.GetFileAsync(Path.GetFileName(filePath), cancellationToken);
        }
        catch (FileNotFoundException)
        {
            try
            {
                // If that fails, try with the full path (in case it's stored with path info)
                stream = await _storage.GetFileAsync(filePath, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                // Finally, try to read directly from the file system
                if (!File.Exists(filePath))
                {
                    _logger.LogError("File not found: {FilePath}", filePath);
                    throw new FileNotFoundException($"Excel file not found: {filePath}", filePath);
                }
                stream = File.OpenRead(filePath);
            }
        }

        var wb = new Workbook(stream);
        var result = new WorkbookEntity();
        foreach (Worksheet sheet in wb.Worksheets)
        {
            var ws = new WorksheetEntity { Name = sheet.Name };
            ws.HeaderRowIndex = DetectHeaderRow(sheet);
            ws.Headers = ExtractHeaders(sheet, ws.HeaderRowIndex);
            ws.Rows = ExtractRows(sheet, ws.HeaderRowIndex);
            result.Worksheets.Add(ws);
        }

        return result;
    }

    private static int DetectHeaderRow(Worksheet sheet)
    {
        var maxRow = Math.Min(sheet.Cells.MaxDataRow, 4);
        var bestRow = 0;
        var bestScore = -1.0;
        for (var r = 0; r <= maxRow; r++)
        {
            var nonEmpty = 0;
            var stringCount = 0;
            for (var c = 0; c <= sheet.Cells.MaxDataColumn; c++)
            {
                var cell = sheet.Cells[r, c];
                if (!string.IsNullOrWhiteSpace(cell.StringValue))
                {
                    nonEmpty++;
                    if (cell.Type == CellValueType.IsString) stringCount++;
                }
            }

            if (sheet.Cells.MaxDataColumn >= 0)
            {
                var ratio = (double)nonEmpty / (sheet.Cells.MaxDataColumn + 1) + stringCount * 0.01;
                if (ratio > bestScore)
                {
                    bestScore = ratio;
                    bestRow = r;
                }
            }
        }

        return bestRow;
    }

    private static List<HeaderEntity> ExtractHeaders(Worksheet sheet, int headerRow)
    {
        var headers = new List<HeaderEntity>();
        for (var c = 0; c <= sheet.Cells.MaxDataColumn; c++)
        {
            var text = sheet.Cells[headerRow, c].StringValue.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            headers.Add(new HeaderEntity
            {
                Name = text,
                Synonyms = GenerateSynonyms(text),
                ColumnIndex = c,
                IsKey = IsKeyColumn(text)
            });
        }

        return headers;
    }

    private static List<List<CellEntity>> ExtractRows(Worksheet sheet, int headerRow)
    {
        var rows = new List<List<CellEntity>>();
        for (var r = headerRow + 1; r <= sheet.Cells.MaxDataRow; r++)
        {
            var row = new List<CellEntity>();
            for (var c = 0; c <= sheet.Cells.MaxDataColumn; c++)
            {
                row.Add(new CellEntity
                {
                    RowIndex = r,
                    ColumnIndex = c,
                    Value = sheet.Cells[r, c].StringValue
                });
            }

            rows.Add(row);
        }

        return rows;
    }

    private static List<string> GenerateSynonyms(string header)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { header };
        var tokens = header.Replace('_', ' ').Split(new[] { ' ', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var t in tokens)
        {
            set.Add(t);
            set.Add(t.ToLowerInvariant());
        }

        if (header.Contains("fecha", StringComparison.OrdinalIgnoreCase)) set.Add("date");
        if (header.Equals("id", StringComparison.OrdinalIgnoreCase)) set.Add("identifier");
        if (header.Contains("mes", StringComparison.OrdinalIgnoreCase)) set.Add("month");
        if (header.Contains("cuenta", StringComparison.OrdinalIgnoreCase)) set.Add("account");

        return set.ToList();
    }

    private static bool IsKeyColumn(string header)
    {
        var lower = header.ToLowerInvariant();
        return lower.Contains("id") || lower.Contains("fecha") || lower.Contains("mes") || lower.Contains("cuenta");
    }
}
