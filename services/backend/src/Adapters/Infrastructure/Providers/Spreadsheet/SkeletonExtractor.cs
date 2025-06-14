using System.Collections.Concurrent;
using Application.Interfaces;
using Aspose.Cells;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Extracts skeleton views from workbooks for compression.
/// </summary>
public sealed class SkeletonExtractor : ISkeletonExtractor
{
    private readonly ILogger<SkeletonExtractor> _logger;

    public SkeletonExtractor(ILogger<SkeletonExtractor> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SkeletonSheet> ExtractSkeletonAsync(
        WorksheetContext worksheet,
        StructuralAnchors anchors,
        SkeletonExtractionOptions? options = null)
    {
        options ??= new SkeletonExtractionOptions();
        
        _logger.LogDebug(
            "Extracting skeleton from worksheet '{WorksheetName}' with {RowAnchors} row anchors and {ColAnchors} column anchors",
            worksheet.Name,
            anchors.AnchorRows.Count,
            anchors.AnchorColumns.Count);

        var skeletonCells = new Dictionary<string, EnhancedCellEntity>();
        var skeletonToOriginal = new Dictionary<string, string>();
        var originalToSkeleton = new Dictionary<string, string>();

        // Build row and column remapping
        var (preservedRows, rowMapping) = BuildIndexMapping(
            anchors.AnchorRows,
            worksheet.Dimensions.FirstRowIndex,
            worksheet.Dimensions.LastRowIndex);
            
        var (preservedCols, colMapping) = BuildIndexMapping(
            anchors.AnchorColumns,
            worksheet.Dimensions.FirstColumnIndex,
            worksheet.Dimensions.LastColumnIndex);

        var addressMapping = new AddressRemap
        {
            SkeletonToOriginal = skeletonToOriginal,
            OriginalToSkeleton = originalToSkeleton,
            RowRemap = rowMapping,
            ColumnRemap = colMapping
        };

        // Extract cells to preserve
        var preservedCellCount = 0;
        var discardedCellCount = 0;

        foreach (var (originalAddress, cell) in worksheet.Cells)
        {
            var shouldPreserve = ShouldPreserveCell(
                cell,
                anchors,
                preservedRows,
                preservedCols,
                options);

            if (shouldPreserve)
            {
                // Calculate new indices
                if (rowMapping.TryGetValue(cell.RowIndex, out var newRow) &&
                    colMapping.TryGetValue(cell.ColumnIndex, out var newCol))
                {
                    var newAddress = CellsHelper.CellIndexToName(newRow, newCol);
                    
                    // Create skeleton cell with remapped coordinates
                    var skeletonCell = new EnhancedCellEntity
                    {
                        Address = newAddress,
                        RowIndex = newRow,
                        ColumnIndex = newCol,
                        Value = cell.Value,
                        NumberFormatString = cell.NumberFormatString,
                        FormattedValue = cell.FormattedValue,
                        DataType = cell.DataType,
                        Formula = cell.Formula,
                        Style = cell.Style
                    };

                    skeletonCells[newAddress] = skeletonCell;
                    skeletonToOriginal[newAddress] = originalAddress;
                    originalToSkeleton[originalAddress] = newAddress;
                    preservedCellCount++;
                }
            }
            else
            {
                discardedCellCount++;
                
                // Optionally create placeholder
                if (options.CreatePlaceholders && IsSignificantCell(cell))
                {
                    // Add to a placeholder tracking structure (not implemented in this version)
                }
            }
        }

        // Calculate compression statistics
        var originalCellCount = worksheet.Cells.Count;
        var compressionRatio = originalCellCount > 0 
            ? (originalCellCount - preservedCellCount) / (double)originalCellCount 
            : 0;

        var stats = new CompressionStats
        {
            OriginalCellCount = originalCellCount,
            SkeletonCellCount = preservedCellCount,
            CompressionRatio = compressionRatio,
            PreservedRows = preservedRows.Count,
            PreservedColumns = preservedCols.Count,
            DiscardedCells = discardedCellCount
        };

        // Check if we met the minimum compression target
        if (compressionRatio < options.MinCompressionRatio)
        {
            _logger.LogWarning(
                "Compression ratio {Ratio:P} is below target {Target:P} for worksheet '{WorksheetName}'",
                compressionRatio,
                options.MinCompressionRatio,
                worksheet.Name);
        }

        _logger.LogInformation(
            "Extracted skeleton for worksheet '{WorksheetName}': {PreservedCells}/{OriginalCells} cells preserved ({CompressionRatio:P} compression)",
            worksheet.Name,
            preservedCellCount,
            originalCellCount,
            compressionRatio);

        return new SkeletonSheet
        {
            OriginalName = worksheet.Name,
            SkeletonCells = skeletonCells,
            AddressMapping = addressMapping,
            Stats = stats
        };
    }

    public async Task<SkeletonWorkbook> ExtractWorkbookSkeletonAsync(
        WorkbookContext context,
        WorkbookAnchors anchors,
        SkeletonExtractionOptions? options = null)
    {
        options ??= new SkeletonExtractionOptions();
        
        _logger.LogDebug("Extracting skeleton for workbook with {SheetCount} sheets", context.Worksheets.Count);

        var skeletonSheets = new ConcurrentBag<SkeletonSheet>();
        var originalMemoryBytes = GC.GetTotalMemory(false);

        // Process sheets in parallel
        var tasks = context.Worksheets.Select(async worksheet =>
        {
            if (anchors.WorksheetAnchors.TryGetValue(worksheet.Name, out var sheetAnchors))
            {
                var skeleton = await ExtractSkeletonAsync(worksheet, sheetAnchors, options);
                skeletonSheets.Add(skeleton);
            }
            else
            {
                _logger.LogWarning("No anchors found for worksheet '{WorksheetName}', skipping", worksheet.Name);
            }
        });

        await Task.WhenAll(tasks);

        // Calculate global statistics
        var totalOriginalCells = skeletonSheets.Sum(s => s.Stats.OriginalCellCount);
        var totalSkeletonCells = skeletonSheets.Sum(s => s.Stats.SkeletonCellCount);
        var globalCompressionRatio = totalOriginalCells > 0
            ? (totalOriginalCells - totalSkeletonCells) / (double)totalOriginalCells
            : 0;

        var skeletonMemoryBytes = GC.GetTotalMemory(false);

        var globalStats = new WorkbookCompressionStats
        {
            OriginalCellCount = totalOriginalCells,
            SkeletonCellCount = totalSkeletonCells,
            CompressionRatio = globalCompressionRatio,
            PreservedRows = skeletonSheets.Sum(s => s.Stats.PreservedRows),
            PreservedColumns = skeletonSheets.Sum(s => s.Stats.PreservedColumns),
            DiscardedCells = skeletonSheets.Sum(s => s.Stats.DiscardedCells),
            OriginalSheetCount = context.Worksheets.Count,
            SkeletonSheetCount = skeletonSheets.Count,
            OriginalMemoryBytes = originalMemoryBytes,
            SkeletonMemoryBytes = skeletonMemoryBytes
        };

        _logger.LogInformation(
            "Extracted workbook skeleton: {SkeletonSheets}/{OriginalSheets} sheets, {SkeletonCells}/{OriginalCells} cells ({CompressionRatio:P} compression)",
            skeletonSheets.Count,
            context.Worksheets.Count,
            totalSkeletonCells,
            totalOriginalCells,
            globalCompressionRatio);

        return new SkeletonWorkbook
        {
            OriginalFilePath = context.FilePath,
            Sheets = skeletonSheets.OrderBy(s => s.OriginalName).ToList(),
            GlobalStats = globalStats
        };
    }

    private (HashSet<int> preservedIndices, Dictionary<int, int> mapping) BuildIndexMapping(
        HashSet<int> anchorIndices,
        int minIndex,
        int maxIndex)
    {
        var preservedIndices = new HashSet<int>(anchorIndices);
        var mapping = new Dictionary<int, int>();
        
        // Always preserve boundary indices
        preservedIndices.Add(minIndex);
        preservedIndices.Add(maxIndex);

        // Sort preserved indices
        var sortedIndices = preservedIndices.OrderBy(i => i).ToList();
        
        // Create mapping from original to new indices
        for (var i = 0; i < sortedIndices.Count; i++)
        {
            mapping[sortedIndices[i]] = i;
        }

        return (preservedIndices, mapping);
    }

    private bool ShouldPreserveCell(
        EnhancedCellEntity cell,
        StructuralAnchors anchors,
        HashSet<int> preservedRows,
        HashSet<int> preservedCols,
        SkeletonExtractionOptions options)
    {
        // Always preserve cells at row/column intersections
        if (preservedRows.Contains(cell.RowIndex) && preservedCols.Contains(cell.ColumnIndex))
        {
            // Additional checks for empty cells
            if (cell.DataType == CellDataType.Empty && !options.PreserveNearbyNonEmpty)
                return false;
                
            return true;
        }

        // Preserve cells in header regions
        foreach (var header in anchors.HeaderRegions)
        {
            if (cell.RowIndex >= header.StartRow && cell.RowIndex <= header.EndRow &&
                cell.ColumnIndex >= header.StartColumn && cell.ColumnIndex <= header.EndColumn)
            {
                return true;
            }
        }

        // Preserve formula cells if requested
        if (options.PreserveFormulas && cell.DataType == CellDataType.Formula)
            return true;

        // Preserve specially formatted cells if requested
        if (options.PreserveFormattedCells && IsSpeciallyFormatted(cell))
            return true;

        return false;
    }

    private bool IsSpeciallyFormatted(EnhancedCellEntity cell)
    {
        if (cell.Style == null) return false;

        return cell.Style.IsBold ||
               cell.Style.IsMerged ||
               !string.IsNullOrEmpty(cell.Style.BackgroundColor) ||
               cell.Style.HasBorders;
    }

    private bool IsSignificantCell(EnhancedCellEntity cell)
    {
        // Cells that might be worth tracking even when discarded
        return cell.DataType == CellDataType.Formula ||
               IsSpeciallyFormatted(cell) ||
               (cell.DataType == CellDataType.String && cell.FormattedValue?.Length > 50);
    }
}