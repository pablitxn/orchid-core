using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.ChainOfSpreadsheet;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using Domain.Entities.Spreadsheet;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace Application.Tests.UseCases.Spreadsheet.ChainOfSpreadsheet;

public class ChainOfSpreadsheetHandlerTests
{
    private readonly Mock<ILogger<ChainOfSpreadsheetHandler>> _loggerMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ITableDetectionService> _tableDetectionMock;
    private readonly Mock<IChatCompletionService> _chatCompletionMock;
    private readonly Mock<ITelemetryClient> _telemetryMock;
    private readonly Mock<IActivityPublisher> _activityMock;
    private readonly Mock<IActionCostRepository> _costRepositoryMock;
    private readonly Mock<IEnhancedWorkbookLoader> _workbookLoaderMock;
    private readonly ChainOfSpreadsheetHandler _handler;

    public ChainOfSpreadsheetHandlerTests()
    {
        _loggerMock = new Mock<ILogger<ChainOfSpreadsheetHandler>>();
        _mediatorMock = new Mock<IMediator>();
        _tableDetectionMock = new Mock<ITableDetectionService>();
        _chatCompletionMock = new Mock<IChatCompletionService>();
        _telemetryMock = new Mock<ITelemetryClient>();
        _activityMock = new Mock<IActivityPublisher>();
        _costRepositoryMock = new Mock<IActionCostRepository>();
        _workbookLoaderMock = new Mock<IEnhancedWorkbookLoader>();

        _handler = new ChainOfSpreadsheetHandler(
            _loggerMock.Object,
            _mediatorMock.Object,
            _tableDetectionMock.Object,
            _chatCompletionMock.Object,
            _telemetryMock.Object,
            _activityMock.Object,
            _costRepositoryMock.Object,
            _workbookLoaderMock.Object);
    }

    [Fact]
    public async Task Handle_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the total sales?",
            CompressionStrategy.Balanced
            );

        var compressedResult = new CompressWorkbookResult
        {
            Success = true,
            CompressedContent = "Sheet1: A1: Product, B1: Sales...",
            EstimatedTokens = 100,
            Statistics = new CompressionStatistics
            {
                OriginalCellCount = 1000,
                CompressedCellCount = 100,
                CompressionRatio = 0.9
            }
        };

        var detectionResult = new TableDetectionResult(
            Tables:
            [
                new Application.Interfaces.Spreadsheet.DetectedTable(
                    SheetName: "Sheet1",
                    TopRow: 1,
                    BottomRow: 10,
                    LeftColumn: 1,
                    RightColumn: 2,
                    ConfidenceScore: 0.95,
                    TableType: "Data",
                    Description: "Sales table"
                )
            ],
            TokensUsed: 50,
            EstimatedCost: 0.01m,
            RawLlmResponse: "Found sales table"
        );

        var workbook = new WorkbookContext
        {
            Worksheets =
            [
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = new Dictionary<string, EnhancedCellEntity>
                    {
                        ["A1"] = new EnhancedCellEntity
                            { Address = "A1", FormattedValue = "Product", RowIndex = 1, ColumnIndex = 1 },
                        ["B1"] = new EnhancedCellEntity
                            { Address = "B1", FormattedValue = "Sales", RowIndex = 1, ColumnIndex = 2 },
                        ["A2"] = new EnhancedCellEntity
                            { Address = "A2", FormattedValue = "Product A", RowIndex = 2, ColumnIndex = 1 },
                        ["B2"] = new EnhancedCellEntity
                            { Address = "B2", FormattedValue = "1000", RowIndex = 2, ColumnIndex = 2 }
                    }
                }
            ]
        };

        var chatMessageContent = Mock.Of<ChatMessageContent>(c => c.Content == "The total sales is 1000");

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compressedResult);

        _tableDetectionMock.Setup(x => x.DetectTablesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(detectionResult);

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessageContent]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("The total sales is 1000");
        result.DetectedTable.Should().Be("Sheet1!A1:B10");
        result.Trace.Should().NotBeNull();
        result.Trace!.TableDetection.Should().NotBeNull();
        result.Trace.QuestionAnswering.Should().NotBeNull();
        result.Error.Should().BeNullOrEmpty();

        _activityMock.Verify(x => x.PublishAsync(
            "chain_of_spreadsheet",
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _costRepositoryMock.Verify(x => x.RecordActionCostAsync(
            "chain_of_spreadsheet_answer",
            It.IsAny<decimal>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CompressionFails_ReturnsErrorResponse()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the total?",
            CompressionStrategy.Balanced);

        var compressedResult = new CompressWorkbookResult
        {
            Success = false,
            CompressedContent = string.Empty
        };

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compressedResult);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Failed to compress spreadsheet");
        result.Answer.Should().BeNull();
        result.DetectedTable.Should().BeNull();
        result.Trace.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NoRelevantTablesFound_UsesFullContextFallback()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the total?");

        var compressedResult = new CompressWorkbookResult
        {
            Success = true,
            CompressedContent = "Sheet1: A1: Value1, A2: Value2..."
        };

        var detectionResult = new TableDetectionResult(
            Tables: new List<Application.Interfaces.Spreadsheet.DetectedTable>(), // No tables detected
            TokensUsed: 30,
            EstimatedCost: 0.005m,
            RawLlmResponse: "No tables found"
        );

        var chatMessageContent =
            Mock.Of<ChatMessageContent>(c => c.Content == "Based on the full context, the answer is 42");

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compressedResult);

        _tableDetectionMock.Setup(x => x.DetectTablesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(detectionResult);

        _chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessageContent]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("Based on the full context, the answer is 42");
        result.DetectedTable.Should().Be("Full spreadsheet context");
        result.Trace.Should().NotBeNull();

        _costRepositoryMock.Verify(x => x.RecordActionCostAsync(
            "chain_of_spreadsheet_full_context_answer",
            It.IsAny<decimal>(),
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExceptionThrown_ReturnsErrorResponse()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the total?");

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Test exception");
        result.Answer.Should().BeNull();
        result.DetectedTable.Should().BeNull();
        result.Trace.Should().BeNull();

        _telemetryMock.Verify(x => x.EndTraceAsync("trace-123", false, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithoutReasoningTrace_DoesNotIncludeTrace()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the total?",
            CompressionStrategy.None,
            false); // Don't include reasoning trace

        var compressedResult = new CompressWorkbookResult
        {
            Success = true,
            CompressedContent = "Sheet1: A1: Total, B1: 500"
        };

        var detectionResult = new TableDetectionResult(
            Tables:
            [
                new Application.Interfaces.Spreadsheet.DetectedTable(
                    SheetName: "Sheet1",
                    TopRow: 1,
                    BottomRow: 1,
                    LeftColumn: 1,
                    RightColumn: 2,
                    ConfidenceScore: 0.9,
                    TableType: null,
                    Description: null
                )
            ],
            TokensUsed: 0,
            EstimatedCost: 0m,
            RawLlmResponse: null
        );

        var workbook = new WorkbookContext
        {
            Worksheets =
            [
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = new Dictionary<string, EnhancedCellEntity>
                    {
                        ["A1"] = new EnhancedCellEntity
                            { Address = "A1", FormattedValue = "Total", RowIndex = 1, ColumnIndex = 1 },
                        ["B1"] = new EnhancedCellEntity
                            { Address = "B1", FormattedValue = "500", RowIndex = 1, ColumnIndex = 2 }
                    }
                }
            ]
        };

        var chatMessageContent = Mock.Of<ChatMessageContent>(c => c.Content == "The total is 500");

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compressedResult);

        _tableDetectionMock.Setup(x => x.DetectTablesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(detectionResult);

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessageContent]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("The total is 500");
        result.Trace.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MultipleTablesDetected_SelectsHighestConfidence()
    {
        // Arrange
        var command = new ChainOfSpreadsheetCommand(
            "test.xlsx",
            "What is the revenue?");

        var compressedResult = new CompressWorkbookResult
        {
            Success = true,
            CompressedContent = "Multiple tables found..."
        };

        var detectionResult = new TableDetectionResult(
            Tables:
            [
                new Application.Interfaces.Spreadsheet.DetectedTable(
                    SheetName: "Sheet1",
                    TopRow: 1,
                    BottomRow: 5,
                    LeftColumn: 1,
                    RightColumn: 3,
                    ConfidenceScore: 0.7,
                    TableType: null,
                    Description: "Expenses table"
                ),

                new Application.Interfaces.Spreadsheet.DetectedTable(
                    SheetName: "Sheet1",
                    TopRow: 10,
                    BottomRow: 15,
                    LeftColumn: 1,
                    RightColumn: 3,
                    ConfidenceScore: 0.95,
                    TableType: null,
                    Description: "Revenue table"
                )
            ],
            TokensUsed: 0,
            EstimatedCost: 0m,
            RawLlmResponse: null
        );

        var workbook = new WorkbookContext
        {
            Worksheets =
            [
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = new Dictionary<string, EnhancedCellEntity>
                    {
                        ["A10"] = new EnhancedCellEntity
                            { Address = "A10", FormattedValue = "Revenue", RowIndex = 10, ColumnIndex = 1 },
                        ["A11"] = new EnhancedCellEntity
                            { Address = "A11", FormattedValue = "Q1", RowIndex = 11, ColumnIndex = 1 },
                        ["B11"] = new EnhancedCellEntity
                            { Address = "B11", FormattedValue = "50000", RowIndex = 11, ColumnIndex = 2 }
                    }
                }
            ]
        };

        var chatMessageContent = Mock.Of<ChatMessageContent>(c => c.Content == "The revenue is 50000");

        _telemetryMock.Setup(x =>
                x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("trace-123");

        _mediatorMock.Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(compressedResult);

        _tableDetectionMock.Setup(x => x.DetectTablesAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(detectionResult);

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _chatCompletionMock.Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([chatMessageContent]);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Answer.Should().Be("The revenue is 50000");
        result.DetectedTable.Should().Be("Sheet1!A10:C15"); // Should select the table with 0.95 confidence
    }
}