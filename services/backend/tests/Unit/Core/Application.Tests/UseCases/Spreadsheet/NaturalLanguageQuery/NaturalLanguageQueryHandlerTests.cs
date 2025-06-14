using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.Spreadsheet.NaturalLanguageQuery;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Spreadsheet.NaturalLanguageQuery;

public class NaturalLanguageQueryHandlerTests
{
    private readonly Mock<ILogger<NaturalLanguageQueryHandler>> _loggerMock;
    private readonly Mock<IWorkbookLoader> _workbookLoaderMock;
    private readonly Mock<IWorkbookNormalizer> _normalizerMock;
    private readonly Mock<IWorkbookSummarizer> _summarizerMock;
    private readonly Mock<IFormulaTranslator> _translatorMock;
    private readonly Mock<IFormulaValidator> _validatorMock;
    private readonly Mock<IFormulaExecutor> _executorMock;
    private readonly Mock<ICacheStore> _cacheMock;
    private readonly Mock<IActivityPublisher> _activityMock;
    private readonly NaturalLanguageQueryHandler _handler;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public NaturalLanguageQueryHandlerTests()
    {
        _loggerMock = new Mock<ILogger<NaturalLanguageQueryHandler>>();
        _workbookLoaderMock = new Mock<IWorkbookLoader>();
        _normalizerMock = new Mock<IWorkbookNormalizer>();
        _summarizerMock = new Mock<IWorkbookSummarizer>();
        _translatorMock = new Mock<IFormulaTranslator>();
        _validatorMock = new Mock<IFormulaValidator>();
        _executorMock = new Mock<IFormulaExecutor>();
        _cacheMock = new Mock<ICacheStore>();
        _activityMock = new Mock<IActivityPublisher>();

        _handler = new NaturalLanguageQueryHandler(
            _loggerMock.Object,
            _workbookLoaderMock.Object,
            _normalizerMock.Object,
            _summarizerMock.Object,
            _translatorMock.Object,
            _validatorMock.Object,
            _executorMock.Object,
            _cacheMock.Object,
            _activityMock.Object);
    }

    [Fact]
    public async Task Handle_SuccessfulQuery_ReturnsSuccessResult()
    {
        // Arrange
        var command = new NaturalLanguageQueryCommand(
            "test.xlsx",
            "What is the total amount?");

        var workbook = CreateTestWorkbook();
        var normalizedWorkbook = CreateTestNormalizedWorkbook();
        var summary = CreateTestSummary();
        var translation = new FormulaTranslation
        {
            Formula = "=SUM(Amount)",
            Explanation = "Calculates the sum of all amounts",
            NeedsClarification = false
        };
        var validation = new FormulaValidation { IsValid = true };
        var formulaResult = new FormulaResult
        {
            Success = true,
            Value = 600.0,
            ResultType = FormulaResultType.SingleValue
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        
        _workbookLoaderMock.Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);
        _normalizerMock.Setup(x => x.NormalizeAsync(It.IsAny<WorkbookEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedWorkbook);
        _summarizerMock.Setup(x => x.SummarizeAsync(It.IsAny<NormalizedWorkbook>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        _translatorMock.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<WorkbookSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translation);
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<NormalizedWorkbook>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validation);
        _executorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formulaResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(600.0, result.Result);
        Assert.Equal("=SUM(Amount)", result.Formula);
        Assert.Equal("Calculates the sum of all amounts", result.Explanation);
        Assert.Null(result.Error);
        
        // Verify cache was set
        _cacheMock.Verify(x => x.SetStringAsync(
            It.IsRegex("normalized-workbook:.*"), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(x => x.SetStringAsync(
            It.IsRegex("workbook-summary:.*"), 
            It.IsAny<string>(), 
            It.IsAny<TimeSpan?>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_QueryNeedsClarification_ReturnsClarificationResponse()
    {
        // Arrange
        var command = new NaturalLanguageQueryCommand(
            "test.xlsx",
            "What is the date?");

        var workbook = CreateTestWorkbook();
        var normalizedWorkbook = CreateTestNormalizedWorkbook();
        var summary = CreateTestSummary();
        var translation = new FormulaTranslation
        {
            Formula = "",
            Explanation = "Multiple date columns found",
            NeedsClarification = true,
            ClarificationPrompt = "Which date are you referring to? Order date or delivery date?"
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        
        _workbookLoaderMock.Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);
        _normalizerMock.Setup(x => x.NormalizeAsync(It.IsAny<WorkbookEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedWorkbook);
        _summarizerMock.Setup(x => x.SummarizeAsync(It.IsAny<NormalizedWorkbook>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        _translatorMock.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<WorkbookSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translation);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.True(result.NeedsClarification);
        Assert.Equal("Which date are you referring to? Order date or delivery date?", result.ClarificationPrompt);
    }

    [Fact]
    public async Task Handle_InvalidFormula_ReturnsErrorResponse()
    {
        // Arrange
        var command = new NaturalLanguageQueryCommand(
            "test.xlsx",
            "Calculate something invalid");

        var workbook = CreateTestWorkbook();
        var normalizedWorkbook = CreateTestNormalizedWorkbook();
        var summary = CreateTestSummary();
        var translation = new FormulaTranslation
        {
            Formula = "=INVALID()",
            Explanation = "Invalid formula",
            NeedsClarification = false
        };
        var validation = new FormulaValidation 
        { 
            IsValid = false,
            Errors = new List<string> { "Unknown function: INVALID" }
        };

        _cacheMock.Setup(x => x.GetStringAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        
        _workbookLoaderMock.Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);
        _normalizerMock.Setup(x => x.NormalizeAsync(It.IsAny<WorkbookEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(normalizedWorkbook);
        _summarizerMock.Setup(x => x.SummarizeAsync(It.IsAny<NormalizedWorkbook>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);
        _translatorMock.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<WorkbookSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translation);
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<NormalizedWorkbook>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validation);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Unknown function: INVALID", result.Error);
    }

    [Fact]
    public async Task Handle_UsesCache_WhenAvailable()
    {
        // Arrange
        var command = new NaturalLanguageQueryCommand(
            "test.xlsx",
            "What is the total?");

        var normalizedWorkbook = CreateTestNormalizedWorkbook();
        var summary = CreateTestSummary();
        var translation = new FormulaTranslation
        {
            Formula = "=SUM(Amount)",
            Explanation = "Sum of amounts",
            NeedsClarification = false
        };
        var validation = new FormulaValidation { IsValid = true };
        var formulaResult = new FormulaResult
        {
            Success = true,
            Value = 600.0,
            ResultType = FormulaResultType.SingleValue
        };

        // Setup cache to return serialized objects
        _cacheMock.Setup(x => x.GetStringAsync(It.IsRegex("normalized-workbook:.*"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(normalizedWorkbook, JsonOptions));
        _cacheMock.Setup(x => x.GetStringAsync(It.IsRegex("workbook-summary:.*"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(JsonSerializer.Serialize(summary, JsonOptions));
        
        _translatorMock.Setup(x => x.TranslateAsync(It.IsAny<string>(), It.IsAny<WorkbookSummary>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(translation);
        _validatorMock.Setup(x => x.ValidateAsync(It.IsAny<string>(), It.IsAny<NormalizedWorkbook>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validation);
        _executorMock.Setup(x => x.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(formulaResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        _workbookLoaderMock.Verify(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _normalizerMock.Verify(x => x.NormalizeAsync(It.IsAny<WorkbookEntity>(), It.IsAny<CancellationToken>()), Times.Never);
        _summarizerMock.Verify(x => x.SummarizeAsync(It.IsAny<NormalizedWorkbook>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static WorkbookEntity CreateTestWorkbook()
    {
        return new WorkbookEntity
        {
            Worksheets = new List<WorksheetEntity>
            {
                new WorksheetEntity
                {
                    Name = "Sheet1",
                    Headers = new List<HeaderEntity>
                    {
                        new HeaderEntity { Name = "Date", ColumnIndex = 0 },
                        new HeaderEntity { Name = "Customer", ColumnIndex = 1 },
                        new HeaderEntity { Name = "Amount", ColumnIndex = 2 }
                    },
                    Rows = new List<List<CellEntity>>()
                }
            }
        };
    }

    private static NormalizedWorkbook CreateTestNormalizedWorkbook()
    {
        return new NormalizedWorkbook
        {
            MainWorksheet = new WorksheetEntity { Name = "Sheet1" },
            ColumnMetadata = new Dictionary<string, ColumnMetadata>
            {
                ["Amount"] = new ColumnMetadata { OriginalName = "Amount", DataType = ColumnDataType.Number }
            }
        };
    }

    private static WorkbookSummary CreateTestSummary()
    {
        return new WorkbookSummary
        {
            SheetName = "Sheet1",
            TotalRows = 100,
            Columns = new List<ColumnSummary>
            {
                new ColumnSummary { Alias = "Amount", Original = "Amount", DataType = "Number" }
            }
        };
    }
}