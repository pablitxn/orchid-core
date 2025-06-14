using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Providers;

public class FormulaValidatorTests
{
    private readonly FormulaValidator _validator;
    private readonly Mock<ILogger<FormulaValidator>> _loggerMock;

    public FormulaValidatorTests()
    {
        _loggerMock = new Mock<ILogger<FormulaValidator>>();
        _validator = new FormulaValidator(_loggerMock.Object);
    }

    [Theory]
    [InlineData("=SUM(A1:A10)", true)]
    [InlineData("=AVERAGE(B:B)", true)]
    [InlineData("=IF(A1>10,\"Yes\",\"No\")", true)]
    [InlineData("SUM(A1:A10)", false)] // Missing =
    [InlineData("=SUM(A1:A10", false)] // Unbalanced parentheses
    [InlineData("", false)] // Empty formula
    public async Task ValidateAsync_BasicSyntax_ReturnsExpectedResult(string formula, bool expectedValid)
    {
        // Arrange
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        Assert.Equal(expectedValid, result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_ValidFormula_ExtractsFunctionsAndRanges()
    {
        // Arrange
        var formula = "=SUM(A1:A10) + AVERAGE(B1:B10)";
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("SUM", result.ReferencedFunctions);
        Assert.Contains("AVERAGE", result.ReferencedFunctions);
        Assert.Contains("A1:A10", result.ReferencedRanges);
        Assert.Contains("B1:B10", result.ReferencedRanges);
    }

    [Fact]
    public async Task ValidateAsync_UnknownFunction_ReturnsInvalid()
    {
        // Arrange
        var formula = "=UNKNOWNFUNC(A1:A10)";
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Unknown function: UNKNOWNFUNC", result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_ValidColumnReference_ReturnsValid()
    {
        // Arrange
        var formula = "=SUM(Amount)";
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_InvalidColumnReference_ReturnsInvalid()
    {
        // Arrange
        var formula = "=SUM(InvalidColumn)";
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid column reference: InvalidColumn", result.Errors);
    }

    [Theory]
    [InlineData("=INDIRECT(A1)", true)]
    [InlineData("=HYPERLINK(\"http://example.com\")", true)]
    [InlineData("=CALL(\"function\")", true)]
    public async Task ValidateAsync_DangerousPatterns_ReturnsInvalid(string formula, bool expectedDangerous)
    {
        // Arrange
        var workbook = CreateTestNormalizedWorkbook();
        
        // Act
        var result = await _validator.ValidateAsync(formula, workbook);
        
        // Assert
        if (expectedDangerous)
        {
            Assert.False(result.IsValid);
            Assert.Contains("Formula contains potentially dangerous patterns", result.Errors);
        }
    }

    private static NormalizedWorkbook CreateTestNormalizedWorkbook()
    {
        return new NormalizedWorkbook
        {
            MainWorksheet = new WorksheetEntity { Name = "Sheet1" },
            AliasToOriginal = new Dictionary<string, string>
            {
                ["Date"] = "Date",
                ["Customer"] = "Customer",
                ["Amount"] = "Amount"
            },
            OriginalToAlias = new Dictionary<string, string>
            {
                ["Date"] = "Date",
                ["Customer"] = "Customer",
                ["Amount"] = "Amount"
            },
            ColumnMetadata = new Dictionary<string, ColumnMetadata>
            {
                ["Date"] = new ColumnMetadata { OriginalName = "Date", CanonicalName = "Date", DataType = ColumnDataType.DateTime },
                ["Customer"] = new ColumnMetadata { OriginalName = "Customer", CanonicalName = "Customer", DataType = ColumnDataType.String },
                ["Amount"] = new ColumnMetadata { OriginalName = "Amount", CanonicalName = "Amount", DataType = ColumnDataType.Number }
            }
        };
    }
}