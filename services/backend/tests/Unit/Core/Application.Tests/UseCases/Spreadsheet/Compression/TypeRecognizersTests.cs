using Application.UseCases.Spreadsheet.Compression;
using FluentAssertions;
using Xunit;

namespace Application.Tests.UseCases.Spreadsheet.Compression;

public class TypeRecognizersTests
{
    [Theory]
    [InlineData("2024-01-15", null, true)]
    [InlineData("15/01/2024", null, true)]
    [InlineData("01-15-2024", null, true)]
    [InlineData("1/15/24", null, true)]
    [InlineData("not a date", null, false)]
    [InlineData("2024", null, false)]
    [InlineData(null, "yyyy-MM-dd", true)]
    [InlineData(null, "dd/mm/yyyy", true)]
    public void DateTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new DateTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("yyyy-mm-dd");
    }
    
    [Theory]
    [InlineData("75%", null, true)]
    [InlineData("100.5%", null, true)]
    [InlineData("-25%", null, true)]
    [InlineData("75", null, false)]
    [InlineData("percent", null, false)]
    [InlineData(null, "0.00%", true)]
    [InlineData(null, "0%", true)]
    public void PercentageTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new PercentageTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("0.00%");
    }
    
    [Theory]
    [InlineData("$1,234.56", null, true)]
    [InlineData("€1,234.56", null, true)]
    [InlineData("£1,234.56", null, true)]
    [InlineData("¥1,234", null, true)]
    [InlineData("1234.56", null, false)]
    [InlineData(null, "$#,##0.00", true)]
    [InlineData(null, "Currency", true)]
    public void CurrencyTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new CurrencyTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("Currency");
    }
    
    [Theory]
    [InlineData("1.23E+10", null, true)]
    [InlineData("5.67e-05", null, true)]
    [InlineData("-3.14E+00", null, true)]
    [InlineData("123", null, false)]
    [InlineData(null, "0.00E+00", true)]
    [InlineData(null, "0.00E-00", true)]
    public void ScientificTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new ScientificTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("0.00E+00");
    }
    
    [Theory]
    [InlineData("14:30", null, true)]
    [InlineData("2:30:45", null, true)]
    [InlineData("14:30 PM", null, true)]
    [InlineData("2:30 AM", null, true)]
    [InlineData("14:30:45:123", null, false)]
    [InlineData(null, "hh:mm:ss", true)]
    [InlineData(null, "h:mm AM/PM", true)]
    public void TimeTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new TimeTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("hh:mm:ss");
    }
    
    [Theory]
    [InlineData("1/2", null, true)]
    [InlineData("3 / 4", null, true)]
    [InlineData("-5/8", null, true)]
    [InlineData("1.5", null, false)]
    [InlineData(null, "# ??/??", true)]
    [InlineData(null, "# ?/?", true)]
    public void FractionTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new FractionTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("# ??/??");
    }
    
    [Theory]
    [InlineData(null, "_($* #,##0.00_)", true)]
    [InlineData(null, "Accounting", true)]
    [InlineData(null, "$#,##0.00", false)]
    [InlineData("1234.56", null, false)]
    public void AccountingTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new AccountingTypeRecognizer();
        
        // Act
        var result = recognizer.CanRecognize(value, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("_($* #,##0.00_)");
    }
    
    [Theory]
    [InlineData("true", null, true)]
    [InlineData("false", null, true)]
    [InlineData("TRUE", null, true)]
    [InlineData("yes", null, true)]
    [InlineData("no", null, true)]
    [InlineData("si", null, true)]
    [InlineData("verdadero", null, true)]
    [InlineData("1", null, true)]
    [InlineData("0", null, true)]
    [InlineData("maybe", null, false)]
    [InlineData("true_bool", null, true)]
    [InlineData("false_bool", null, true)]
    public void BooleanTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new BooleanTypeRecognizer();
        
        // Handle special test cases for actual boolean values
        object? actualValue = value switch
        {
            "true_bool" => true,
            "false_bool" => false,
            _ => value
        };
        
        // Act
        var result = recognizer.CanRecognize(actualValue, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("Boolean");
    }
    
    [Theory]
    [InlineData("1,234.56", null, true)]
    [InlineData("-1,234", null, true)]
    [InlineData("123", null, true)]
    [InlineData("int_123", null, true)]
    [InlineData("double_123.45", null, true)]
    [InlineData("decimal_123.45", null, true)]
    [InlineData("text", null, false)]
    [InlineData(null, "#,##0.00", true)]
    [InlineData(null, "General", true)]
    public void NumberTypeRecognizer_RecognizesCorrectly(object? value, string? format, bool expected)
    {
        // Arrange
        var recognizer = new NumberTypeRecognizer();
        
        // Handle special test cases for actual numeric values
        object? actualValue = value switch
        {
            "int_123" => 123,
            "double_123.45" => 123.45,
            "decimal_123.45" => 123.45m,
            _ => value
        };
        
        // Act
        var result = recognizer.CanRecognize(actualValue, format);
        
        // Assert
        result.Should().Be(expected);
        recognizer.GetTypeToken().Should().Be("#,##0.00");
    }
}