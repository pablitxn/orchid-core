using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Executes Excel formulas in a safe, isolated environment.
/// </summary>
public interface IFormulaExecutor
{
    /// <summary>
    /// Executes a formula and returns the result.
    /// </summary>
    Task<FormulaResult> ExecuteAsync(
        string formula,
        string filePath,
        string worksheetName,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}