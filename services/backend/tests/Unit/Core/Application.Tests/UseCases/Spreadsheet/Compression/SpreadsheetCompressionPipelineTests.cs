using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.Compression;
using Domain.ValueObjects.Spreadsheet;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Application.Tests.UseCases.Spreadsheet.Compression;

public class SpreadsheetCompressionPipelineTests
{
    private readonly ServiceProvider _serviceProvider;

    public SpreadsheetCompressionPipelineTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IInvertedIndexTranslator, InvertedIndexTranslator>();
        services.AddScoped<IFormatAwareAggregator, FormatAwareAggregator>();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pipeline_SingleStep_ExecutesSuccessfully()
    {
        // Arrange
        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddInvertedIndex()
            .Build();

        var workbook = CreateTestWorkbook();

        // Act
        var result = await pipeline.ExecuteAsync(workbook);

        // Assert
        result.Should().NotBeNull();
        result.CompressedText.Should().NotBeEmpty();
        result.StepTimings.Should().ContainKey("InvertedIndex");

        // For small datasets, compression may not always reduce size
        // Just verify that the ratio is calculated correctly
        var expectedRatio = (double)result.OriginalTokenCount / result.CompressedTokenCount;
        result.CompressionRatio.Should().BeApproximately(expectedRatio, 0.01);
    }

    [Fact]
    public async Task Pipeline_MultipleSteps_ExecutesInOrder()
    {
        // Arrange
        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddInvertedIndex()
            .AddFormatAggregation()
            .Build();

        var workbook = CreateTestWorkbook();

        // Act
        var result = await pipeline.ExecuteAsync(workbook);

        // Assert
        result.StepTimings.Should().HaveCount(2);
        result.StepTimings.Should().ContainKey("InvertedIndex");
        result.StepTimings.Should().ContainKey("FormatAggregation");
        result.Artifacts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Pipeline_CustomStep_IntegratesCorrectly()
    {
        // Arrange
        var customStep = new Mock<IPipelineStep>();
        customStep.Setup(s => s.Name).Returns("CustomStep");
        customStep.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineStepResult
            {
                Success = true,
                Outputs = new Dictionary<string, object> { ["CustomOutput"] = "TestValue" },
                ExecutionTime = TimeSpan.FromMilliseconds(100)
            });

        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddStep(customStep.Object)
            .Build();

        var workbook = CreateTestWorkbook();

        // Act
        var result = await pipeline.ExecuteAsync(workbook);

        // Assert
        customStep.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
        result.StepTimings.Should().ContainKey("CustomStep");
    }

    [Fact]
    public async Task Pipeline_StepFailure_StopsExecution()
    {
        // Arrange
        var failingStep = new Mock<IPipelineStep>();
        failingStep.Setup(s => s.Name).Returns("FailingStep");
        failingStep.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineStepResult
            {
                Success = false,
                Error = "Test error",
                ExecutionTime = TimeSpan.FromMilliseconds(50)
            });

        var successStep = new Mock<IPipelineStep>();
        successStep.Setup(s => s.Name).Returns("SuccessStep");

        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddStep(failingStep.Object)
            .AddStep(successStep.Object)
            .Build();

        var workbook = CreateTestWorkbook();

        // Act
        var result = await pipeline.ExecuteAsync(workbook);

        // Assert
        failingStep.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
        successStep.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        result.StepTimings.Should().HaveCount(1);
    }

    [Fact]
    public async Task Pipeline_Performance_Under1SecondFor50kCells()
    {
        // Arrange
        var largeCells = new List<CellData>();
        for (int i = 0; i < 50000; i++)
        {
            largeCells.Add(new CellData
            {
                Address = new CellAddress(i / 100, i % 100),
                Value = $"Value{i}",
                NumberFormatString = i % 2 == 0 ? "$#,##0.00" : "0.00%"
            });
        }

        var workbook = new WorkbookContext
        {
            Name = "LargeWorkbook",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = largeCells,
                    Dimensions = (500, 100)
                }
            }
        };

        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddInvertedIndex()
            .AddFormatAggregation()
            .Build();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await pipeline.ExecuteAsync(workbook);
        stopwatch.Stop();

        // Assert
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
        result.CompressionRatio.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void PipelineBuilder_NoSteps_ThrowsException()
    {
        // Arrange
        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);

        // Act & Assert
        builder.Invoking(b => b.Build())
            .Should().Throw<InvalidOperationException>()
            .WithMessage("Pipeline must have at least one step");
    }

    [Fact]
    public async Task Pipeline_IntermediateResults_PassedBetweenSteps()
    {
        // Arrange
        var step1 = new Mock<IPipelineStep>();
        step1.Setup(s => s.Name).Returns("Step1");
        step1.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext ctx, CancellationToken _) =>
            {
                ctx.IntermediateResults["Output1"] = "TestValue";
                return new PipelineStepResult
                {
                    Success = true,
                    Outputs = new Dictionary<string, object> { ["Output1"] = "Value1" },
                    ExecutionTime = TimeSpan.FromMilliseconds(10)
                };
            });

        var step2 = new Mock<IPipelineStep>();
        step2.Setup(s => s.Name).Returns("Step2");
        step2.Setup(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PipelineContext ctx, CancellationToken _) =>
            {
                // Verify intermediate result from step1
                ctx.IntermediateResults.Should().ContainKey("Output1");
                return new PipelineStepResult
                {
                    Success = true,
                    ExecutionTime = TimeSpan.FromMilliseconds(10)
                };
            });

        var builder = new SpreadsheetPipelineBuilder(_serviceProvider);
        var pipeline = builder
            .AddStep(step1.Object)
            .AddStep(step2.Object)
            .Build();

        var workbook = CreateTestWorkbook();

        // Act
        await pipeline.ExecuteAsync(workbook);

        // Assert
        step2.Verify(s => s.ExecuteAsync(It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WorkbookContext CreateTestWorkbook()
    {
        return new WorkbookContext
        {
            Name = "TestWorkbook",
            Worksheets = new[]
            {
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Cells = new[]
                    {
                        new CellData { Address = new CellAddress("A1"), Value = "Invoice#" },
                        new CellData { Address = new CellAddress("A2"), Value = "INV-001" },
                        new CellData
                            { Address = new CellAddress("B1"), Value = "Amount", NumberFormatString = "$#,##0.00" },
                        new CellData
                            { Address = new CellAddress("B2"), Value = 1000.50m, NumberFormatString = "$#,##0.00" }
                    },
                    Dimensions = (2, 2)
                },
                new WorksheetContext
                {
                    Name = "Sheet2",
                    Cells = new[]
                    {
                        new CellData { Address = new CellAddress("A1"), Value = "Date" },
                        new CellData { Address = new CellAddress("A2"), Value = "2024-01-15" }
                    },
                    Dimensions = (2, 1)
                }
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = 6,
                EmptyCells = 0,
                TypeDistribution = new Dictionary<Type, int>
                {
                    [typeof(string)] = 4,
                    [typeof(decimal)] = 1
                }
            }
        };
    }
}