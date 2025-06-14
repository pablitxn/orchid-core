using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Summarizes workbook data for LLM consumption.
/// </summary>
public sealed class WorkbookSummarizer(ILogger<WorkbookSummarizer> logger) : IWorkbookSummarizer
{
    private readonly ILogger<WorkbookSummarizer> _logger = logger;

    public Task<WorkbookSummary> SummarizeAsync(
        NormalizedWorkbook workbook,
        int sampleSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating workbook summary with sample size {SampleSize}", sampleSize);
        
        var summary = new WorkbookSummary
        {
            SheetName = workbook.MainWorksheet.Name,
            AliasToOriginal = workbook.AliasToOriginal,
            TotalRows = workbook.MainWorksheet.Rows.Count
        };
        
        // Build column summaries
        summary.Columns = workbook.ColumnMetadata.Select(kvp => new ColumnSummary
        {
            Alias = kvp.Key,
            Original = kvp.Value.OriginalName,
            DataType = kvp.Value.DataType.ToString(),
            Min = FormatValue(kvp.Value.MinValue, kvp.Value.DataType),
            Max = FormatValue(kvp.Value.MaxValue, kvp.Value.DataType),
            Mean = kvp.Value.Mean,
            UniqueCount = kvp.Value.UniqueCount,
            TopValues = kvp.Value.TopValues.Take(5).ToList()
        }).ToList();
        
        // Get sample rows
        var rows = workbook.MainWorksheet.Rows;
        var sampleIndices = GetSampleIndices(rows.Count, sampleSize);
        
        summary.SampleRows = sampleIndices
            .Select(idx => BuildRowDictionary(rows[idx], workbook))
            .ToList();
        
        // Generate compact JSON
        var compactData = new
        {
            sheet = summary.SheetName,
            totalRows = summary.TotalRows,
            columns = summary.Columns.Select(c => new
            {
                name = c.Alias,
                type = c.DataType.ToLower(),
                min = CompactNumber(c.Min),
                max = CompactNumber(c.Max),
                unique = c.UniqueCount
            }),
            sample = summary.SampleRows
        };
        
        summary.CompactJson = JsonSerializer.Serialize(compactData, new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        _logger.LogInformation("Generated summary: {ByteSize} bytes", summary.CompactJson.Length);
        
        return Task.FromResult(summary);
    }

    private static List<int> GetSampleIndices(int totalRows, int sampleSize)
    {
        var indices = new HashSet<int>();
        
        // First N rows
        for (int i = 0; i < Math.Min(sampleSize, totalRows); i++)
        {
            indices.Add(i);
        }
        
        // Last N rows
        for (int i = Math.Max(0, totalRows - sampleSize); i < totalRows; i++)
        {
            indices.Add(i);
        }
        
        // Random rows from middle
        if (totalRows > sampleSize * 3)
        {
            var random = new Random();
            var middleStart = sampleSize;
            var middleEnd = totalRows - sampleSize;
            
            for (int i = 0; i < sampleSize && indices.Count < sampleSize * 3; i++)
            {
                var idx = random.Next(middleStart, middleEnd);
                indices.Add(idx);
            }
        }
        
        return indices.OrderBy(i => i).ToList();
    }

    private static Dictionary<string, string> BuildRowDictionary(
        List<CellEntity> row,
        NormalizedWorkbook workbook)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var cell in row)
        {
            var header = workbook.MainWorksheet.Headers
                .FirstOrDefault(h => h.ColumnIndex == cell.ColumnIndex);
            
            if (header != null && workbook.OriginalToAlias.TryGetValue(header.Name, out var alias))
            {
                dict[alias] = cell.Value;
            }
        }
        
        return dict;
    }

    private static object? FormatValue(object? value, ColumnDataType dataType)
    {
        if (value == null) return null;
        
        return dataType switch
        {
            ColumnDataType.DateTime when value is DateTime dt => dt.ToString("yyyy-MM-dd"),
            ColumnDataType.Number when value is double d => Math.Round(d, 2),
            _ => value
        };
    }

    private static object? CompactNumber(object? value)
    {
        if (value is double d)
        {
            if (d >= 1_000_000)
                return $"{d / 1_000_000:0.#}M";
            if (d >= 1_000)
                return $"{d / 1_000:0.#}K";
            return Math.Round(d, 2);
        }
        return value;
    }
}