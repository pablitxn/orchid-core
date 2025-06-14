using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.ChainOfSpreadsheet;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using Domain.Entities;
using Domain.Entities.Spreadsheet;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using DetectedTable = Application.Interfaces.Spreadsheet.DetectedTable;

namespace Spreadsheet.IntegrationTests
{
    public class ChainOfSpreadsheetTests
    {
        private readonly Mock<IMediator> _mockMediator;
        private readonly Mock<ITableDetectionService> _mockTableDetection;
        private readonly Mock<IChatCompletionService> _mockChatCompletion;
        private readonly Mock<ITelemetryClient> _mockTelemetry;
        private readonly Mock<IActivityPublisher> _mockActivity;
        private readonly Mock<IEnhancedWorkbookLoader> _mockWorkbookLoader;
        private readonly ChainOfSpreadsheetHandler _handler;

        public ChainOfSpreadsheetTests()
        {
            _mockMediator = new Mock<IMediator>();
            _mockTableDetection = new Mock<ITableDetectionService>();
            _mockChatCompletion = new Mock<IChatCompletionService>();
            _mockTelemetry = new Mock<ITelemetryClient>();
            _mockActivity = new Mock<IActivityPublisher>();
            var mockCostRepository = new Mock<IActionCostRepository>();
            _mockWorkbookLoader = new Mock<IEnhancedWorkbookLoader>();

            var logger = new Mock<ILogger<ChainOfSpreadsheetHandler>>();

            _handler = new ChainOfSpreadsheetHandler(
                logger.Object,
                _mockMediator.Object,
                _mockTableDetection.Object,
                _mockChatCompletion.Object,
                _mockTelemetry.Object,
                _mockActivity.Object,
                mockCostRepository.Object,
                _mockWorkbookLoader.Object);
        }

        [Fact]
        public async Task Handle_WithDetectedTable_ReturnsAnswerBasedOnTable()
        {
            // Arrange
            var command = new ChainOfSpreadsheetCommand(
                "test.xlsx",
                "What is the total sales for Q1?");

            var compressResult = new CompressWorkbookResult
            {
                Success = true,
                CompressedContent = "Compressed data...",
                EstimatedTokens = 100,
                Statistics = new CompressionStatistics
                {
                    OriginalCellCount = 1000,
                    CompressedCellCount = 100,
                    CompressionRatio = 10,
                    SheetsProcessed = 1,
                    ProcessingTimeMs = 50
                }
            };

            _mockMediator
                .Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(compressResult);

            var detectedTable = new DetectedTable(
                "Sales", 1, 1, 10, 5, 0.95, "sales", "Quarterly sales data");

            _mockTableDetection
                .Setup(x => x.DetectTablesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TableDetectionResult(
                    [detectedTable],
                    150,
                    0.002m,
                    "Raw LLM response"));

            // Build a minimal in-memory workbook context compatible with the enhanced loader
            var worksheet = new WorksheetContext
            {
                Name = "Sales",
                Index = 0,
                Cells = new Dictionary<string, EnhancedCellEntity>
                {
                    ["A1"] = new EnhancedCellEntity
                    {
                        Address = "A1", RowIndex = 0, ColumnIndex = 0, Value = "Product", FormattedValue = "Product",
                        DataType = CellDataType.String
                    },
                    ["B1"] = new EnhancedCellEntity
                    {
                        Address = "B1", RowIndex = 0, ColumnIndex = 1, Value = "Q1", FormattedValue = "Q1",
                        DataType = CellDataType.String
                    },
                    ["A2"] = new EnhancedCellEntity
                    {
                        Address = "A2", RowIndex = 1, ColumnIndex = 0, Value = "Widget A", FormattedValue = "Widget A",
                        DataType = CellDataType.String
                    },
                    ["B2"] = new EnhancedCellEntity
                    {
                        Address = "B2", RowIndex = 1, ColumnIndex = 1, Value = 1000m, FormattedValue = "1000",
                        DataType = CellDataType.Number
                    }
                }
            };

            var workbook = new WorkbookContext
            {
                FilePath = "test.xlsx",
                Worksheets = [worksheet]
            };

            _mockWorkbookLoader
                .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<WorkbookLoadOptions?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(workbook);

            _mockChatCompletion
                .Setup(x => x.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ChatMessageContent(AuthorRole.Assistant, "The total sales for Q1 is 1000.")
                ]);

            _mockTelemetry
                .Setup(x => x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("trace-123");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("The total sales for Q1 is 1000.", result.Answer);
            Assert.Equal("Sales!A1:E10", result.DetectedTable);
            Assert.NotNull(result.Trace);
            Assert.Equal(1, result.Trace.TableDetection.TablesDetected);

            // Verify activity was published
            _mockActivity.Verify(x => x.PublishAsync(
                "chain_of_spreadsheet",
                It.Is<object>(o => o.ToString()!.Contains("Question")),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Handle_WithNoTablesDetected_UsesFullContext()
        {
            // Arrange
            var command = new ChainOfSpreadsheetCommand(
                "test.xlsx",
                "What is the data about?",
                CompressionStrategy.Balanced,
                false);

            var compressResult = new CompressWorkbookResult
            {
                Success = true,
                CompressedContent = "Simple data: A1: Hello, A2: World",
                EstimatedTokens = 10
            };

            _mockMediator
                .Setup(x => x.Send(It.IsAny<CompressWorkbookCommand>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(compressResult);

            _mockTableDetection
                .Setup(x => x.DetectTablesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TableDetectionResult(
                    new List<DetectedTable>(), // No tables detected
                    50,
                    0.001m,
                    "No tables found"));

            _mockChatCompletion
                .Setup(x => x.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([
                    new ChatMessageContent(AuthorRole.Assistant, "The data contains simple text values.")
                ]);

            _mockTelemetry
                .Setup(x => x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("trace-123");

            // Act
            var result = await _handler.Handle(command, CancellationToken.None);

            // Assert
            Assert.True(result.Success);
            Assert.Equal("The data contains simple text values.", result.Answer);
            Assert.Equal("Full spreadsheet context", result.DetectedTable);
            Assert.Null(result.Trace); // includeTrace was false
        }
    }
}