namespace Application.UseCases.Spreadsheet.NaturalLanguageQuery;

/// <summary>
/// Response from natural language query execution.
/// </summary>
public sealed record NaturalLanguageQueryResponse(
    bool Success,
    object? Result,
    string Formula,
    string Explanation,
    string? Error = null,
    bool NeedsClarification = false,
    string? ClarificationPrompt = null);