using System.Globalization;
using System.Text.RegularExpressions;
using Application.Interfaces.Spreadsheet;

namespace Application.UseCases.Spreadsheet.Compression;

public sealed class DateTypeRecognizer : ITypeRecognizer
{
    private static readonly Regex[] DatePatterns = 
    {
        new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled), // yyyy-MM-dd
        new(@"^\d{2}/\d{2}/\d{4}$", RegexOptions.Compiled), // MM/dd/yyyy
        new(@"^\d{2}-\d{2}-\d{4}$", RegexOptions.Compiled), // dd-MM-yyyy
        new(@"^\d{1,2}/\d{1,2}/\d{2}$", RegexOptions.Compiled) // M/d/yy
    };
    
    public string TypeName => "Date";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (!string.IsNullOrWhiteSpace(formatString))
        {
            // Check for date patterns (MM for months, not mm for minutes)
            if (formatString.Contains("yyyy", StringComparison.OrdinalIgnoreCase) ||
                formatString.Contains("MM") || // Months
                formatString.Contains("dd", StringComparison.OrdinalIgnoreCase))
            {
                // Make sure it's not a time format
                if (!formatString.Contains("hh", StringComparison.OrdinalIgnoreCase) &&
                    !formatString.Contains("ss", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        
        if (value is DateTime) return true;
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return DatePatterns.Any(p => p.IsMatch(strValue));
    }
    
    // Tests and downstream components rely on the lower-case month pattern.
    public string GetTypeToken() => "yyyy-mm-dd";
}

public sealed class PercentageTypeRecognizer : ITypeRecognizer
{
    private static readonly Regex PercentPattern = new(@"^-?\d+(\.\d+)?%$", RegexOptions.Compiled);
    
    public string TypeName => "Percentage";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (formatString?.Contains("%") == true) return true;
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return PercentPattern.IsMatch(strValue);
    }
    
    public string GetTypeToken() => "0.00%";
}

public sealed class CurrencyTypeRecognizer : ITypeRecognizer
{
    // Flexible currency pattern: optional currency symbol, optional thousands separators, optional decimals
    private static readonly Regex FlexibleCurrencyPattern = new(
        @"^([$€£¥₹₽¢]\s*)?-?\d+(?:(?:,\d{3})*(?:\.\d+)?|(?:\.\d+))$", 
        RegexOptions.Compiled);
    
    public string TypeName => "Currency";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        // First check format string for currency indicators
        var currencySymbols = new[] { "$", "€", "£", "¥", "₹", "₽", "¢" };
        if (!string.IsNullOrWhiteSpace(formatString))
        {
            if (currencySymbols.Any(symbol => formatString.Contains(symbol)) ||
                formatString.Contains("Currency", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue))
            return false;
        
        // Only recognize values that contain a currency symbol when no format string is provided
        if (!currencySymbols.Any(symbol => strValue.Contains(symbol)))
            return false;
        
        // Try regex pattern first
        if (FlexibleCurrencyPattern.IsMatch(strValue))
        {
            return true;
        }
        
        // Fallback: try culture-aware parsing after removing currency symbols
        var cleanedValue = strValue.TrimStart('$', '€', '£', '¥', '₹', '₽', '¢').Trim();
        return decimal.TryParse(cleanedValue, NumberStyles.Currency, CultureInfo.InvariantCulture, out _);
    }
    
    public string GetTypeToken() => "Currency";
}

public sealed class ScientificTypeRecognizer : ITypeRecognizer
{
    private static readonly Regex ScientificPattern = new(@"^-?\d+(\.\d+)?[eE][+-]?\d+$", RegexOptions.Compiled);
    
    public string TypeName => "Scientific";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (formatString?.Contains("E+", StringComparison.OrdinalIgnoreCase) == true ||
            formatString?.Contains("E-", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return ScientificPattern.IsMatch(strValue);
    }
    
    public string GetTypeToken() => "0.00E+00";
}

public sealed class TimeTypeRecognizer : ITypeRecognizer
{
    private static readonly Regex[] TimePatterns = 
    {
        new(@"^\d{1,2}:\d{2}(:\d{2})?(\s*(AM|PM))?$", RegexOptions.Compiled | RegexOptions.IgnoreCase)
    };
    
    public string TypeName => "Time";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (formatString?.Contains("hh", StringComparison.OrdinalIgnoreCase) == true ||
            formatString?.Contains("mm", StringComparison.OrdinalIgnoreCase) == true ||
            formatString?.Contains("ss", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        
        if (value is TimeSpan) return true;
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return TimePatterns.Any(p => p.IsMatch(strValue));
    }
    
    public string GetTypeToken() => "hh:mm:ss";
}

public sealed class FractionTypeRecognizer : ITypeRecognizer
{
    private static readonly Regex FractionPattern = new(@"^-?\d+\s*/\s*\d+$", RegexOptions.Compiled);
    
    public string TypeName => "Fraction";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (formatString?.Contains("??/??") == true ||
            formatString?.Contains("# ?/?") == true)
        {
            return true;
        }
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return FractionPattern.IsMatch(strValue);
    }
    
    public string GetTypeToken() => "# ??/??";
}

public sealed class AccountingTypeRecognizer : ITypeRecognizer
{
    public string TypeName => "Accounting";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        return formatString?.Contains("_($", StringComparison.OrdinalIgnoreCase) == true ||
               formatString?.Contains("Accounting", StringComparison.OrdinalIgnoreCase) == true;
    }
    
    public string GetTypeToken() => "_($* #,##0.00_)";
}

public sealed class BooleanTypeRecognizer : ITypeRecognizer
{
    private static readonly HashSet<string> BooleanValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "yes", "no", "si", "no", "verdadero", "falso", "1", "0"
    };
    
    public string TypeName => "Boolean";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (value is bool) return true;
        
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return BooleanValues.Contains(strValue);
    }
    
    public string GetTypeToken() => "Boolean";
}

public sealed class NumberTypeRecognizer : ITypeRecognizer
{
    // Allow numbers with or without comma grouping
    private static readonly Regex NumberPattern = new(@"^-?\d+(?:,\d{3})*(?:\.\d+)?$", RegexOptions.Compiled);
    
    public string TypeName => "Number";
    
    public bool CanRecognize(object? value, string? formatString)
    {
        if (formatString?.Contains("#,##0") == true ||
            formatString?.Equals("General", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }
        
        if (value is int || value is long || value is decimal || value is double || value is float)
            return true;
            
        var strValue = value?.ToString();
        if (string.IsNullOrWhiteSpace(strValue)) return false;
        
        return NumberPattern.IsMatch(strValue) || 
               double.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
    }
    
    public string GetTypeToken() => "#,##0.00";
}