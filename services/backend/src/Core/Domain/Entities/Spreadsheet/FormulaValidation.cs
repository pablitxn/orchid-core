namespace Domain.Entities.Spreadsheet;

/// <summary>
/// Result of formula validation.
/// </summary>
public class FormulaValidation
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; init; } = [];
    public List<string> ReferencedRanges { get; set; } = [];
    public List<string> ReferencedFunctions { get; set; } = [];
}