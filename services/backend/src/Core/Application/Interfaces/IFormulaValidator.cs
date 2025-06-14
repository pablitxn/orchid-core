using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Validates Excel formulas before execution.
/// </summary>
public interface IFormulaValidator
{
    /// <summary>
    /// Validates a formula for syntax and range references.
    /// </summary>
    Task<FormulaValidation> ValidateAsync(
        string formula,
        NormalizedWorkbook workbook,
        CancellationToken cancellationToken = default);
}