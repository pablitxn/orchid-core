using Domain.Entities.Spreadsheet;

namespace Application.Interfaces;

using Domain.Entities;

/// <summary>
/// Translates natural language queries to Excel formulas using LLM.
/// </summary>
public interface IFormulaTranslator
{
    /// <summary>
    /// Translates a natural language query to an Excel formula.
    /// </summary>
    Task<FormulaTranslation> TranslateAsync(
        string query,
        WorkbookSummary summary,
        CancellationToken cancellationToken = default);
}