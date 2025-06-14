using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Normalizes workbook data including header detection, type inference, and named range creation.
/// </summary>
public sealed class WorkbookNormalizer(ILogger<WorkbookNormalizer> logger) : IWorkbookNormalizer
{
    private readonly ILogger<WorkbookNormalizer> _logger = logger;
    private static readonly Dictionary<string, string> CommonAliases = new()
    {
        { "fecha", "Date" },
        { "mes", "Month" },
        { "año", "Year" },
        { "cliente", "Customer" },
        { "proveedor", "Supplier" },
        { "producto", "Product" },
        { "cantidad", "Quantity" },
        { "precio", "Price" },
        { "total", "Total" },
        { "cuenta", "Account" },
        { "descripcion", "Description" },
        { "descripción", "Description" },
        { "codigo", "Code" },
        { "código", "Code" }
    };

    public Task<NormalizedWorkbook> NormalizeAsync(WorkbookEntity workbook, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Normalizing workbook with {SheetCount} sheets", workbook.Worksheets.Count);
        
        var result = new NormalizedWorkbook
        {
            OriginalWorkbook = workbook
        };
        
        // Detect main table (sheet with most data)
        var mainSheet = DetectMainTable(workbook);
        result.MainWorksheet = mainSheet;
        
        // Build column metadata
        var columnMetadata = new Dictionary<string, ColumnMetadata>();
        var aliasToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var originalToAlias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var header in mainSheet.Headers)
        {
            var canonicalName = GenerateCanonicalName(header.Name);
            var metadata = InferColumnType(mainSheet, header);
            metadata.CanonicalName = canonicalName;
            
            columnMetadata[canonicalName] = metadata;
            aliasToOriginal[canonicalName] = header.Name;
            originalToAlias[header.Name] = canonicalName;
            
            // Add common aliases
            foreach (var synonym in header.Synonyms)
            {
                if (!aliasToOriginal.ContainsKey(synonym))
                {
                    aliasToOriginal[synonym] = header.Name;
                }
            }
        }
        
        result.ColumnMetadata = columnMetadata;
        result.AliasToOriginal = aliasToOriginal;
        result.OriginalToAlias = originalToAlias;
        
        _logger.LogInformation("Normalized {ColumnCount} columns in main table '{SheetName}'", 
            columnMetadata.Count, mainSheet.Name);
        
        return Task.FromResult(result);
    }

    private WorksheetEntity DetectMainTable(WorkbookEntity workbook)
    {
        var bestSheet = workbook.Worksheets[0];
        var bestScore = 0;
        
        foreach (var sheet in workbook.Worksheets)
        {
            var score = sheet.Rows.Count * sheet.Headers.Count;
            if (score > bestScore)
            {
                bestScore = score;
                bestSheet = sheet;
            }
        }
        
        _logger.LogDebug("Detected main table: {SheetName} with score {Score}", bestSheet.Name, bestScore);
        return bestSheet;
    }

    private string GenerateCanonicalName(string original)
    {
        // Remove special characters and normalize
        var normalized = Regex.Replace(original, @"[^\w\s]", "");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        
        // Check for common aliases
        var lowerNorm = normalized.ToLowerInvariant();
        if (CommonAliases.TryGetValue(lowerNorm, out var alias))
        {
            return alias;
        }
        
        // Convert to StudlyCaps
        return string.Join("", normalized.Split(' ')
            .Select(word => char.ToUpper(word[0]) + word.Substring(1).ToLower()));
    }

    private ColumnMetadata InferColumnType(WorksheetEntity sheet, HeaderEntity header)
    {
        var metadata = new ColumnMetadata
        {
            OriginalName = header.Name,
            ColumnIndex = header.ColumnIndex
        };
        
        var values = sheet.Rows
            .Select(row => row.FirstOrDefault(c => c.ColumnIndex == header.ColumnIndex)?.Value ?? string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToList();
        
        if (!values.Any())
        {
            metadata.DataType = ColumnDataType.String;
            return metadata;
        }
        
        // Type inference
        var numericCount = values.Count(v => double.TryParse(v, out _));
        var dateCount = values.Count(v => DateTime.TryParse(v, out _));
        var boolCount = values.Count(v => bool.TryParse(v, out _));
        var totalCount = values.Count;
        
        // Determine type based on majority (>80%)
        if (numericCount > totalCount * 0.8)
        {
            metadata.DataType = ColumnDataType.Number;
            var numbers = values
                .Where(v => double.TryParse(v, out _))
                .Select(v => double.Parse(v))
                .ToList();
            
            if (numbers.Any())
            {
                metadata.MinValue = numbers.Min();
                metadata.MaxValue = numbers.Max();
                metadata.Mean = numbers.Average();
            }
        }
        else if (dateCount > totalCount * 0.8)
        {
            metadata.DataType = ColumnDataType.DateTime;
            var dates = values
                .Where(v => DateTime.TryParse(v, out _))
                .Select(v => DateTime.Parse(v))
                .ToList();
            
            if (dates.Any())
            {
                metadata.MinValue = dates.Min();
                metadata.MaxValue = dates.Max();
            }
        }
        else if (boolCount > totalCount * 0.8)
        {
            metadata.DataType = ColumnDataType.Boolean;
        }
        else
        {
            metadata.DataType = ColumnDataType.String;
        }
        
        // Common metadata
        metadata.UniqueCount = values.Distinct().Count();
        metadata.TopValues = values
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key)
            .ToList();
        
        return metadata;
    }
}