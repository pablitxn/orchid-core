using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Infrastructure.Ai.TableDetection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Spreadsheet.IntegrationTests
{
    public class TableDetectionServiceTests
    {
        private readonly Mock<IChatCompletionService> _mockChatCompletion;
        private readonly Mock<ITelemetryClient> _mockTelemetry;
        private readonly Mock<IActionCostRepository> _mockCostRepository;
        private readonly TableDetectionService _service;

        public TableDetectionServiceTests()
        {
            _mockChatCompletion = new Mock<IChatCompletionService>();
            _mockTelemetry = new Mock<ITelemetryClient>();
            _mockCostRepository = new Mock<IActionCostRepository>();

            var logger = new Mock<ILogger<TableDetectionService>>();
            var options = Options.Create(new TableDetectionOptions
            {
                MaxRetries = 3,
                InputTokenCostPer1K = 0.01m,
                OutputTokenCostPer1K = 0.03m
            });

            _service = new TableDetectionService(
                logger.Object,
                _mockChatCompletion.Object,
                options,
                _mockTelemetry.Object,
                _mockCostRepository.Object);
        }

        [Fact]
        public async Task DetectTablesAsync_WithValidResponse_ReturnsDetectedTables()
        {
            // Arrange
            var compressedText = @"Sheet: Sales
Data:
Cell A1: Product
Cell B1: Q1
Cell C1: Q2
Cell A2: Widget A
Cell B2: 1000
Cell C2: 1200";

            var llmResponse = @"{
                ""tables"": [{
                    ""sheet"": ""Sales"",
                    ""top"": 1,
                    ""left"": 1,
                    ""bottom"": 2,
                    ""right"": 3,
                    ""confidence"": 0.95,
                    ""type"": ""sales"",
                    ""description"": ""Quarterly sales by product""
                }]
            }";

            _mockChatCompletion
                .Setup(x => x.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[]
                {
                    new ChatMessageContent(AuthorRole.Assistant, llmResponse)
                });

            _mockTelemetry
                .Setup(x => x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("trace-123");

            // Act
            var result = await _service.DetectTablesAsync(compressedText);

            // Assert
            Assert.Single(result.Tables);
            var table = result.Tables[0];
            Assert.Equal("Sales", table.SheetName);
            Assert.Equal(1, table.TopRow);
            Assert.Equal(1, table.LeftColumn);
            Assert.Equal(2, table.BottomRow);
            Assert.Equal(3, table.RightColumn);
            Assert.Equal(0.95, table.ConfidenceScore);
            Assert.Equal("Sales!A1:C2", table.GetA1Range());

            // Verify telemetry was called
            _mockTelemetry.Verify(
                x => x.StartTraceAsync("TableDetection", It.IsAny<object>(), It.IsAny<CancellationToken>()),
                Times.Once);
            // _mockTelemetry.Verify(x => x.EndTraceAsync("trace-123", true, It.IsAny<CancellationToken>()), Times.Once);

            // Verify cost was recorded
            _mockCostRepository.Verify(x => x.RecordActionCostAsync(
                "table_detection",
                It.IsAny<decimal>(),
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DetectTablesAsync_WithRetryableError_RetriesAndSucceeds()
        {
            // Arrange
            var compressedText = "Test data";
            var llmResponse = @"{""tables"": []}";
            var callCount = 0;

            _mockChatCompletion
                .Setup(x => x.GetChatMessageContentsAsync(
                    It.IsAny<ChatHistory>(),
                    It.IsAny<PromptExecutionSettings>(),
                    It.IsAny<Kernel>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    callCount++;
                    if (callCount < 2)
                    {
                        throw new HttpRequestException("Temporary error");
                    }

                    return new[]
                    {
                        new ChatMessageContent(AuthorRole.Assistant, llmResponse)
                    };
                });

            _mockTelemetry
                .Setup(x => x.StartTraceAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("trace-123");

            // Act
            var result = await _service.DetectTablesAsync(compressedText);

            // Assert
            Assert.Empty(result.Tables);
            Assert.Equal(2, callCount); // Initial call + 1 retry
        }
    }
}