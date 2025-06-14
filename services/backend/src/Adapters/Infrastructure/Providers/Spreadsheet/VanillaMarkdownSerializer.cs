using System.Buffers;
using System.Text;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Serializes workbook data to vanilla Markdown-like format for LLM consumption.
/// </summary>
public sealed class VanillaMarkdownSerializer : IVanillaSerializer
{
    private readonly ILogger<VanillaMarkdownSerializer> _logger;
    private const int InitialBufferSize = 4096;
    
    public VanillaMarkdownSerializer(ILogger<VanillaMarkdownSerializer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ReadOnlyMemory<char> Serialize(WorkbookContext context, VanillaSerializationOptions? options = null)
    {
        options ??= new VanillaSerializationOptions();
        
        _logger.LogDebug("Serializing workbook with {SheetCount} sheets", context.Worksheets.Count);
        
        var sb = new StringBuilder(InitialBufferSize);
        var cellCount = 0;

        foreach (var worksheet in context.Worksheets)
        {
            if (cellCount > 0)
                sb.Append(options.RowSeparator);
                
            sb.AppendLine($"## Sheet: {worksheet.Name}");
            sb.Append(options.RowSeparator);

            var serializedWorksheet = SerializeWorksheetInternal(worksheet, options, ref cellCount);
            sb.Append(serializedWorksheet);
        }

        var result = sb.ToString();
        _logger.LogDebug("Serialized {CellCount} cells into {CharCount} characters", cellCount, result.Length);
        
        return result.AsMemory();
    }

    public ReadOnlyMemory<char> SerializeWorksheet(WorksheetContext worksheet, VanillaSerializationOptions? options = null)
    {
        options ??= new VanillaSerializationOptions();
        var cellCount = 0;
        var result = SerializeWorksheetInternal(worksheet, options, ref cellCount);
        
        _logger.LogDebug("Serialized worksheet '{WorksheetName}' with {CellCount} cells", worksheet.Name, cellCount);
        
        return result.AsMemory();
    }

    public int EstimateTokenCount(WorkbookContext context, VanillaSerializationOptions? options = null)
    {
        options ??= new VanillaSerializationOptions();
        
        // Use a dummy serialization to get exact character count
        var serialized = Serialize(context, options);
        var byteCount = Encoding.UTF8.GetByteCount(serialized.Span);
        
        // Rough estimation: ~4 bytes per token for English text
        var estimatedTokens = byteCount / 4;
        
        _logger.LogDebug("Estimated {TokenCount} tokens for {ByteCount} bytes", estimatedTokens, byteCount);
        
        return estimatedTokens;
    }

    private string SerializeWorksheetInternal(
        WorksheetContext worksheet, 
        VanillaSerializationOptions options,
        ref int cellCount)
    {
        var sb = new StringBuilder();
        
        // Get dimensions
        var dims = worksheet.Dimensions;
        if (dims.TotalCells == 0)
        {
            sb.AppendLine("(empty worksheet)");
            return sb.ToString();
        }

        // Sort cells by row then column for consistent output
        var sortedCells = worksheet.Cells
            .OrderBy(kvp => kvp.Value.RowIndex)
            .ThenBy(kvp => kvp.Value.ColumnIndex)
            .ToList();

        var currentRow = -1;
        var rowBuilder = new StringBuilder();

        foreach (var (address, cell) in sortedCells)
        {
            // Check max cell limit
            if (options.MaxCells.HasValue && cellCount >= options.MaxCells.Value)
            {
                sb.AppendLine($"{options.RowSeparator}... (truncated at {cellCount} cells)");
                break;
            }

            // Handle row transitions
            if (cell.RowIndex != currentRow)
            {
                if (currentRow >= 0)
                {
                    sb.AppendLine(rowBuilder.ToString());
                    rowBuilder.Clear();
                }
                currentRow = cell.RowIndex;
            }

            // Skip empty cells if requested
            if (!options.IncludeEmptyCells && cell.DataType == CellDataType.Empty)
                continue;

            // Build cell representation
            if (rowBuilder.Length > 0)
                rowBuilder.Append(options.CellSeparator);

            rowBuilder.Append(FormatCell(cell, options));
            cellCount++;
        }

        // Append last row
        if (rowBuilder.Length > 0)
            sb.AppendLine(rowBuilder.ToString());

        return sb.ToString();
    }

    private string FormatCell(EnhancedCellEntity cell, VanillaSerializationOptions options)
    {
        var parts = ArrayPool<string>.Shared.Rent(5);
        var partCount = 0;

        try
        {
            // Always include address
            parts[partCount++] = cell.Address;

            // Include value
            parts[partCount++] = FormatCellValue(cell);

            // Include number format if requested
            if (options.IncludeNumberFormats && !string.IsNullOrEmpty(cell.NumberFormatString))
            {
                parts[partCount++] = cell.NumberFormatString;
            }

            // Include formula if requested
            if (options.IncludeFormulas && !string.IsNullOrEmpty(cell.Formula))
            {
                parts[partCount++] = $"={cell.Formula}";
            }

            // Include style if requested
            if (options.IncludeStyles && cell.Style != null)
            {
                parts[partCount++] = FormatStyle(cell.Style);
            }

            // Join parts
            return string.Join(options.CellSeparator, parts, 0, partCount);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(parts);
        }
    }

    private static string FormatCellValue(EnhancedCellEntity cell)
    {
        return cell.DataType switch
        {
            CellDataType.Empty => "(empty)",
            CellDataType.Error => $"#ERROR: {cell.Value}",
            CellDataType.Formula when cell.Value != null => cell.Value.ToString() ?? "",
            _ => cell.FormattedValue
        };
    }

    private static string FormatStyle(CellStyleMetadata style)
    {
        var styleInfo = new List<string>();
        
        if (style.IsBold) styleInfo.Add("bold");
        if (style.IsMerged) styleInfo.Add($"merged({style.MergedRowSpan}x{style.MergedColumnSpan})");
        if (style.HasBorders) styleInfo.Add("borders");
        if (!string.IsNullOrEmpty(style.BackgroundColor)) styleInfo.Add($"bg:{style.BackgroundColor}");
        
        return styleInfo.Count > 0 ? $"[{string.Join(",", styleInfo)}]" : "";
    }
}