namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Result of formula execution.
/// </summary>
public class FormulaResult
{
    public bool Success { get; set; }
    public object? Value { get; set; }
    public string? Error { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public FormulaResultType ResultType { get; set; }
    public List<List<object>>? MatrixValue { get; set; }
}

/// <summary>
/// Type of formula result.
/// </summary>
public enum FormulaResultType
{
    SingleValue,
    Array,
    Matrix,
    Error
}