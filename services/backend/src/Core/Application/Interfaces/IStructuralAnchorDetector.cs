using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Detects structural anchors in worksheets for intelligent compression.
/// </summary>
public interface IStructuralAnchorDetector
{
    /// <summary>
    /// Finds structural anchor points in a worksheet.
    /// </summary>
    /// <param name="worksheet">Worksheet to analyze.</param>
    /// <param name="k">Number of neighboring rows/columns to preserve around anchors.</param>
    /// <param name="options">Detection options.</param>
    /// <returns>Detected structural anchors.</returns>
    Task<StructuralAnchors> FindAnchorsAsync(
        WorksheetContext worksheet, 
        int k = 2,
        AnchorDetectionOptions? options = null);

    /// <summary>
    /// Analyzes workbook-wide patterns for anchor detection.
    /// </summary>
    Task<WorkbookAnchors> FindWorkbookAnchorsAsync(
        WorkbookContext context,
        int k = 2,
        AnchorDetectionOptions? options = null);
}

/// <summary>
/// Structural anchors detected in a worksheet.
/// </summary>
public sealed class StructuralAnchors
{
    /// <summary>
    /// Row indices identified as structural anchors.
    /// </summary>
    public HashSet<int> AnchorRows { get; init; } = new();

    /// <summary>
    /// Column indices identified as structural anchors.
    /// </summary>
    public HashSet<int> AnchorColumns { get; init; } = new();

    /// <summary>
    /// Detected header regions.
    /// </summary>
    public List<HeaderRegion> HeaderRegions { get; init; } = new();

    /// <summary>
    /// Detection metrics.
    /// </summary>
    public AnchorMetrics Metrics { get; init; } = new();
}

/// <summary>
/// Workbook-level anchor analysis.
/// </summary>
public sealed class WorkbookAnchors
{
    public Dictionary<string, StructuralAnchors> WorksheetAnchors { get; init; } = new();
    public List<CrossSheetPattern> CrossSheetPatterns { get; init; } = new();
    public WorkbookAnchorMetrics GlobalMetrics { get; init; } = new();
}

/// <summary>
/// Detected header region.
/// </summary>
public sealed class HeaderRegion
{
    public int StartRow { get; init; }
    public int EndRow { get; init; }
    public int StartColumn { get; init; }
    public int EndColumn { get; init; }
    public double Confidence { get; init; }
    public HeaderType Type { get; init; }
}

public enum HeaderType
{
    Simple,
    MultiLevel,
    Pivoted,
    Mixed
}

/// <summary>
/// Cross-sheet pattern detection.
/// </summary>
public sealed class CrossSheetPattern
{
    public string PatternName { get; init; } = string.Empty;
    public List<string> AffectedSheets { get; init; } = new();
    public double Confidence { get; init; }
}

/// <summary>
/// Anchor detection metrics.
/// </summary>
public class AnchorMetrics
{
    public int TotalAnchors { get; init; }
    public double AnchorDensity { get; init; }
    public double AverageAnchorDistance { get; init; }
    public Dictionary<string, double> HeterogeneityScores { get; init; } = new();
}

/// <summary>
/// Workbook-level anchor metrics.
/// </summary>
public sealed class WorkbookAnchorMetrics : AnchorMetrics
{
    public int TotalWorksheets { get; init; }
    public double CrossSheetConsistency { get; init; }
}

/// <summary>
/// Options for anchor detection.
/// </summary>
public sealed class AnchorDetectionOptions
{
    /// <summary>
    /// Minimum heterogeneity score to consider as anchor.
    /// </summary>
    public double MinHeterogeneityScore { get; init; } = 0.7;

    /// <summary>
    /// Consider style changes as heterogeneity signals.
    /// </summary>
    public bool ConsiderStyles { get; init; } = true;

    /// <summary>
    /// Consider number format changes.
    /// </summary>
    public bool ConsiderNumberFormats { get; init; } = true;

    /// <summary>
    /// Detect multi-level headers.
    /// </summary>
    public bool DetectMultiLevelHeaders { get; init; } = true;

    /// <summary>
    /// Maximum header depth to detect.
    /// </summary>
    public int MaxHeaderDepth { get; init; } = 3;
}