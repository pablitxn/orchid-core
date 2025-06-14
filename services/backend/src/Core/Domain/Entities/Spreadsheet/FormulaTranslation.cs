namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Result of translating a natural language query to an Excel formula.
/// </summary>
public class FormulaTranslation
{
    public string Formula { get; init; } = string.Empty;
    public string Explanation { get; init; } = string.Empty;
    public bool NeedsClarification { get; init; }
    public string? ClarificationPrompt { get; init; }
}