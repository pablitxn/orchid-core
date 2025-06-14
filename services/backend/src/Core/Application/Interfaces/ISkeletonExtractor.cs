using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Extracts skeleton views from workbooks for compression.
/// </summary>
public interface ISkeletonExtractor
{
    /// <summary>
    /// Extracts a skeleton view based on structural anchors.
    /// </summary>
    /// <param name="worksheet">Original worksheet.</param>
    /// <param name="anchors">Detected anchors.</param>
    /// <param name="options">Extraction options.</param>
    /// <returns>Skeleton worksheet with address remapping.</returns>
    Task<SkeletonSheet> ExtractSkeletonAsync(
        WorksheetContext worksheet,
        StructuralAnchors anchors,
        SkeletonExtractionOptions? options = null);

    /// <summary>
    /// Extracts skeleton for entire workbook.
    /// </summary>
    Task<SkeletonWorkbook> ExtractWorkbookSkeletonAsync(
        WorkbookContext context,
        WorkbookAnchors anchors,
        SkeletonExtractionOptions? options = null);
}

/// <summary>
/// Skeleton view of a worksheet.
/// </summary>
public sealed class SkeletonSheet
{
    /// <summary>
    /// Original worksheet name.
    /// </summary>
    public string OriginalName { get; init; } = string.Empty;

    /// <summary>
    /// Compacted cells preserving structural anchors.
    /// </summary>
    public Dictionary<string, EnhancedCellEntity> SkeletonCells { get; init; } = new();

    /// <summary>
    /// Maps skeleton addresses to original addresses.
    /// </summary>
    public AddressRemap AddressMapping { get; init; } = new();

    /// <summary>
    /// Compression statistics.
    /// </summary>
    public CompressionStats Stats { get; init; } = new();
}

/// <summary>
/// Skeleton view of entire workbook.
/// </summary>
public sealed class SkeletonWorkbook
{
    public string OriginalFilePath { get; init; } = string.Empty;
    public List<SkeletonSheet> Sheets { get; init; } = new();
    public WorkbookCompressionStats GlobalStats { get; init; } = new();
}

/// <summary>
/// Bidirectional address remapping.
/// </summary>
public sealed class AddressRemap
{
    /// <summary>
    /// Maps skeleton address to original address.
    /// </summary>
    public Dictionary<string, string> SkeletonToOriginal { get; init; } = new();

    /// <summary>
    /// Maps original address to skeleton address (if preserved).
    /// </summary>
    public Dictionary<string, string> OriginalToSkeleton { get; init; } = new();

    /// <summary>
    /// Row index remapping.
    /// </summary>
    public Dictionary<int, int> RowRemap { get; init; } = new();

    /// <summary>
    /// Column index remapping.
    /// </summary>
    public Dictionary<int, int> ColumnRemap { get; init; } = new();
}

/// <summary>
/// Compression statistics for a sheet.
/// </summary>
public class CompressionStats
{
    public int OriginalCellCount { get; init; }
    public int SkeletonCellCount { get; init; }
    public double CompressionRatio { get; init; }
    public int PreservedRows { get; init; }
    public int PreservedColumns { get; init; }
    public int DiscardedCells { get; init; }
}

/// <summary>
/// Workbook-level compression statistics.
/// </summary>
public sealed class WorkbookCompressionStats : CompressionStats
{
    public int OriginalSheetCount { get; init; }
    public int SkeletonSheetCount { get; init; }
    public long OriginalMemoryBytes { get; init; }
    public long SkeletonMemoryBytes { get; init; }
}

/// <summary>
/// Options for skeleton extraction.
/// </summary>
public sealed class SkeletonExtractionOptions
{
    /// <summary>
    /// Preserve all non-empty cells within k distance of anchors.
    /// </summary>
    public bool PreserveNearbyNonEmpty { get; init; } = true;

    /// <summary>
    /// Preserve all cells with formulas.
    /// </summary>
    public bool PreserveFormulas { get; init; } = true;

    /// <summary>
    /// Preserve all cells with special formatting.
    /// </summary>
    public bool PreserveFormattedCells { get; init; } = true;

    /// <summary>
    /// Minimum compression ratio target.
    /// </summary>
    public double MinCompressionRatio { get; init; } = 0.7;

    /// <summary>
    /// Create placeholder entries for discarded regions.
    /// </summary>
    public bool CreatePlaceholders { get; init; } = false;
}