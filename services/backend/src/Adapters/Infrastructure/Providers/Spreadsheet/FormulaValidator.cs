using System.Text.RegularExpressions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
/// Validates Excel formulas before execution.
/// </summary>
public sealed class FormulaValidator(ILogger<FormulaValidator> logger) : IFormulaValidator
{
    private readonly ILogger<FormulaValidator> _logger = logger;
    
    // Common Excel functions
    private static readonly HashSet<string> ValidFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        "SUM", "SUMIF", "SUMIFS", "COUNT", "COUNTIF", "COUNTIFS", "AVERAGE", "AVERAGEIF", "AVERAGEIFS",
        "MIN", "MAX", "IF", "IFS", "AND", "OR", "NOT", "VLOOKUP", "HLOOKUP", "INDEX", "MATCH",
        "CONCATENATE", "CONCAT", "LEFT", "RIGHT", "MID", "LEN", "TRIM", "UPPER", "LOWER",
        "DATE", "TODAY", "NOW", "YEAR", "MONTH", "DAY", "WEEKDAY", "DATEDIF",
        "ROUND", "ROUNDUP", "ROUNDDOWN", "ABS", "POWER", "SQRT", "LOG", "LN", "EXP",
        "MEDIAN", "MODE", "STDEV", "VAR", "PERCENTILE", "QUARTILE",
        "ISNUMBER", "ISTEXT", "ISBLANK", "ISERROR", "ISNA"
    };

    public Task<FormulaValidation> ValidateAsync(
        string formula,
        NormalizedWorkbook workbook,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Validating formula: {Formula}", formula);
        
        var validation = new FormulaValidation { IsValid = true };
        
        // Basic syntax validation
        if (string.IsNullOrWhiteSpace(formula))
        {
            validation.IsValid = false;
            validation.Errors.Add("Formula cannot be empty");
            return Task.FromResult(validation);
        }
        
        // Formula should start with =
        if (!formula.TrimStart().StartsWith("="))
        {
            validation.IsValid = false;
            validation.Errors.Add("Formula must start with '='");
            return Task.FromResult(validation);
        }
        
        // Check parentheses balance
        if (!AreParenthesesBalanced(formula))
        {
            validation.IsValid = false;
            validation.Errors.Add("Unbalanced parentheses in formula");
        }
        
        // Extract and validate functions
        var functions = ExtractFunctions(formula);
        validation.ReferencedFunctions = functions;
        
        foreach (var func in functions)
        {
            if (!ValidFunctions.Contains(func))
            {
                validation.Errors.Add($"Unknown function: {func}");
                validation.IsValid = false;
            }
        }
        
        // Extract and validate range references
        var ranges = ExtractRanges(formula);
        validation.ReferencedRanges = ranges;
        
        // Validate column references against workbook
        var columnReferences = ExtractColumnReferences(formula);
        foreach (var colRef in columnReferences)
        {
            if (!IsValidColumnReference(colRef, workbook))
            {
                validation.Errors.Add($"Invalid column reference: {colRef}");
                validation.IsValid = false;
            }
        }
        
        // Check for dangerous patterns
        if (ContainsDangerousPatterns(formula))
        {
            validation.Errors.Add("Formula contains potentially dangerous patterns");
            validation.IsValid = false;
        }
        
        _logger.LogInformation("Formula validation complete. Valid: {IsValid}, Errors: {ErrorCount}", 
            validation.IsValid, validation.Errors.Count);
        
        return Task.FromResult(validation);
    }

    private static bool AreParenthesesBalanced(string formula)
    {
        int count = 0;
        foreach (char c in formula)
        {
            if (c == '(') count++;
            else if (c == ')') count--;
            if (count < 0) return false;
        }
        return count == 0;
    }

    private static List<string> ExtractFunctions(string formula)
    {
        var functions = new List<string>();
        var pattern = @"\b([A-Z]+[A-Z0-9]*)\s*\(";
        var matches = Regex.Matches(formula, pattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            functions.Add(match.Groups[1].Value);
        }
        
        return functions.Distinct().ToList();
    }

    private static List<string> ExtractRanges(string formula)
    {
        var ranges = new List<string>();
        // Match patterns like A1:B10, Sheet1!A:A, etc.
        var pattern = @"(?:[\w]+!)?[A-Z]+\d*:[A-Z]+\d*";
        var matches = Regex.Matches(formula, pattern, RegexOptions.IgnoreCase);
        
        foreach (Match match in matches)
        {
            ranges.Add(match.Value);
        }
        
        return ranges.Distinct().ToList();
    }

    private static List<string> ExtractColumnReferences(string formula)
    {
        var references = new List<string>();
        // Match column names that might be aliases
        var pattern = @"\b([A-Za-z_]\w*)\b(?!\s*\()";
        var matches = Regex.Matches(formula, pattern);
        
        foreach (Match match in matches)
        {
            var value = match.Groups[1].Value;
            // Exclude Excel keywords and cell references
            if (!IsExcelKeyword(value) && !IsCellReference(value))
            {
                references.Add(value);
            }
        }
        
        return references.Distinct().ToList();
    }

    private static bool IsValidColumnReference(string reference, NormalizedWorkbook workbook)
    {
        // Check if it's a valid column alias or original name
        return workbook.AliasToOriginal.ContainsKey(reference) ||
               workbook.OriginalToAlias.ContainsKey(reference);
    }

    private static bool IsExcelKeyword(string value)
    {
        var keywords = new[] { "TRUE", "FALSE", "NULL", "NA", "REF", "VALUE", "NAME", "NUM", "DIV" };
        return keywords.Contains(value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsCellReference(string value)
    {
        // Simple check for A1-style references
        return Regex.IsMatch(value, @"^[A-Z]+\d+$", RegexOptions.IgnoreCase);
    }

    private static bool ContainsDangerousPatterns(string formula)
    {
        // Check for potentially dangerous patterns
        var dangerousPatterns = new[]
        {
            @"INDIRECT\s*\(",  // Can reference arbitrary cells
            @"HYPERLINK\s*\(", // Can create links
            @"CALL\s*\(",      // Can call external functions
            @"REGISTER\.ID\s*\(" // Can register DLLs
        };
        
        return dangerousPatterns.Any(pattern => 
            Regex.IsMatch(formula, pattern, RegexOptions.IgnoreCase));
    }
}