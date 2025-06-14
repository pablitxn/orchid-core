using System.Threading;
using System.Threading.Tasks;
using Aspose.Cells;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Service for executing formulas and calculations on spreadsheet data
/// </summary>
public interface IFormulaExecutionService
{
    /// <summary>
    /// Executes a query with smart retry strategies
    /// </summary>
    Task<QueryExecutionResult> ExecuteQueryAsync(
        Workbook workbook,
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines the best formula strategy for a query
    /// </summary>
    Task<FormulaStrategy> DetermineFormulaStrategyAsync(
        Worksheet sandbox,
        SandboxContext context,
        string query,
        QueryAnalysisResult analysis,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a formula on a worksheet
    /// </summary>
    Task<FormulaExecutionResult> ExecuteFormulaAsync(
        Workbook workbook,
        Worksheet worksheet,
        string formula,
        string targetCell = "Z1",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs manual calculations when formulas fail
    /// </summary>
    Task<QueryExecutionResult> CalculateManuallyAsync(
        Worksheet worksheet,
        string query,
        QueryAnalysisResult analysis,
        SandboxContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and corrects query results
    /// </summary>
    Task<QueryExecutionResult> ValidateResultAsync(
        QueryExecutionResult result,
        SandboxContext context,
        QueryAnalysisResult analysis,
        string query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of query execution
/// </summary>
public sealed class QueryExecutionResult
{
    public bool Success { get; set; }
    public string Query { get; set; } = "";
    public object? Value { get; set; }
    public string? Formula { get; set; }
    public string? Explanation { get; set; }
    public string? Error { get; set; }
    public double Confidence { get; set; }
    public string ExecutionStrategy { get; set; } = "";
    public System.Collections.Generic.Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of formula execution
/// </summary>
public sealed class FormulaExecutionResult
{
    public bool Success { get; set; }
    public object? Value { get; set; }
    public string? FormattedValue { get; set; }
    public string? Error { get; set; }
    public string? ErrorType { get; set; }
    public double ExecutionTimeMs { get; set; }
}

/// <summary>
/// Execution strategy types
/// </summary>
public enum ExecutionStrategyType
{
    StandardFormula,
    HelperColumns,
    ManualCalculation,
    SubQueries,
    Hybrid
}