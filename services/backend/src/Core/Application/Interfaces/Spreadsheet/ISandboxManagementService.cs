using System.Threading;
using System.Threading.Tasks;
using Aspose.Cells;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Service for managing sandbox worksheets for safe spreadsheet operations
/// </summary>
public interface ISandboxManagementService
{
    /// <summary>
    /// Creates a sandbox worksheet with data based on analysis requirements
    /// </summary>
    Task<SandboxCreationResult> CreateSandboxAsync(
        Workbook workbook,
        Worksheet sourceSheet,
        QueryAnalysisResult analysisResult,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies helper columns to a sandbox worksheet
    /// </summary>
    Task ApplyHelperColumnsAsync(
        Worksheet sandbox,
        FormulaStrategy strategy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up a sandbox worksheet
    /// </summary>
    void CleanupSandbox(Workbook workbook, Worksheet sandbox);

    /// <summary>
    /// Validates sandbox data integrity
    /// </summary>
    Task<bool> ValidateSandboxAsync(
        Worksheet sandbox,
        SandboxContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of sandbox creation
/// </summary>
public sealed class SandboxCreationResult
{
    public Worksheet Sandbox { get; set; } = null!;
    public SandboxContext Context { get; set; } = null!;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Context information about a sandbox worksheet
/// </summary>
public sealed class SandboxContext
{
    public int OriginalRowCount { get; set; }
    public int FilteredRowCount { get; set; }
    public System.Collections.Generic.Dictionary<string, int> ColumnStats { get; set; } = new();
    public System.Collections.Generic.List<FilterCriteria> AppliedFilters { get; set; } = new();
    public bool FullDatasetPreserved { get; set; }
    public string SandboxName { get; set; } = "";
    public System.DateTime CreatedAt { get; set; }
}

/// <summary>
/// Formula execution strategy
/// </summary>
public sealed class FormulaStrategy
{
    public string Approach { get; set; } = "";
    public string Formula { get; set; } = "";
    public System.Collections.Generic.List<HelperColumn> HelperColumns { get; set; } = new();
    public string Explanation { get; set; } = "";
    public double Confidence { get; set; }
}

/// <summary>
/// Helper column definition
/// </summary>
public sealed class HelperColumn
{
    public string Name { get; set; } = "";
    public string Formula { get; set; } = "";
    public string Purpose { get; set; } = "";
    public int ColumnIndex { get; set; }
}