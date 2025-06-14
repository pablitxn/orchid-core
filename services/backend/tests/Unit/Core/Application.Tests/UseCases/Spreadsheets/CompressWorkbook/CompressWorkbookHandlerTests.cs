using Application.Interfaces;
using Application.UseCases.Spreadsheets.CompressWorkbook;
using Domain.Entities.Spreadsheet;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.Spreadsheets.CompressWorkbook;

public class CompressWorkbookHandlerTests
{
    private readonly Mock<IEnhancedWorkbookLoader> _workbookLoaderMock;
    private readonly Mock<IVanillaSerializer> _vanillaSerializerMock;
    private readonly Mock<IStructuralAnchorDetector> _anchorDetectorMock;
    private readonly Mock<ISkeletonExtractor> _skeletonExtractorMock;
    private readonly Mock<ILogger<CompressWorkbookHandler>> _loggerMock;
    private readonly CompressWorkbookHandler _handler;

    public CompressWorkbookHandlerTests()
    {
        _workbookLoaderMock = new Mock<IEnhancedWorkbookLoader>();
        _vanillaSerializerMock = new Mock<IVanillaSerializer>();
        _anchorDetectorMock = new Mock<IStructuralAnchorDetector>();
        _skeletonExtractorMock = new Mock<ISkeletonExtractor>();
        _loggerMock = new Mock<ILogger<CompressWorkbookHandler>>();

        _handler = new CompressWorkbookHandler(
            _workbookLoaderMock.Object,
            _vanillaSerializerMock.Object,
            _anchorDetectorMock.Object,
            _skeletonExtractorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_NoCompression_ReturnsVanillaSerializedContent()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.None,
            IncludeFormatting = true,
            IncludeFormulas = true
        };

        var workbook = CreateTestWorkbook();
        var serializedContent = "Sheet1\nA1: Product\nB1: Sales";

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(serializedContent.AsMemory());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CompressedContent.Should().Be("Sheet1\nA1: Product\nB1: Sales");
        result.EstimatedTokens.Should().BeGreaterThan(0);
        result.Statistics.Should().NotBeNull();
        result.Statistics.OriginalCellCount.Should().Be(1000);
        result.Statistics.CompressedCellCount.Should().Be(100);
        result.Statistics.SheetsProcessed.Should().Be(1);
        result.Warnings.Should().BeEmpty();

        _vanillaSerializerMock.Verify(x => x.Serialize(
            It.IsAny<WorkbookContext>(),
            It.Is<VanillaSerializationOptions>(opts =>
                opts.IncludeFormulas == true &&
                opts.IncludeNumberFormats == true &&
                opts.IncludeStyles == true &&
                opts.IncludeEmptyCells == false)), Times.Once);
    }

    [Fact]
    public async Task Handle_BalancedCompression_AppliesAnchorAndSkeletonExtraction()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.Balanced,
            IncludeFormatting = false,
            IncludeFormulas = false
        };

        var workbook = CreateTestWorkbook();
        var anchors = new WorkbookAnchors
        {
            WorksheetAnchors = new Dictionary<string, StructuralAnchors>
            {
                ["Sheet1"] = new StructuralAnchors
                {
                    AnchorRows = [1],
                    AnchorColumns = [1]
                }
            }
        };
        var skeleton = new SkeletonWorkbook
        {
            OriginalFilePath = "test.xlsx",
            Sheets =
            [
                new SkeletonSheet
                {
                    OriginalName = "Sheet1",
                    SkeletonCells = new Dictionary<string, EnhancedCellEntity>
                    {
                        ["A1"] = new EnhancedCellEntity { Address = "A1", FormattedValue = "Product" }
                    }
                }
            ],
            GlobalStats = new WorkbookCompressionStats
            {
                OriginalCellCount = 1000,
                SkeletonCellCount = 50,
                CompressionRatio = 0.95
            }
        };
        var serializedContent = "Compressed: A1: Product";

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _anchorDetectorMock.Setup(x => x.FindWorkbookAnchorsAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<int>(),
                It.IsAny<AnchorDetectionOptions>()))
            .ReturnsAsync(anchors);

        _skeletonExtractorMock.Setup(x => x.ExtractWorkbookSkeletonAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<WorkbookAnchors>(),
                It.IsAny<SkeletonExtractionOptions>()))
            .ReturnsAsync(skeleton);

        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(serializedContent.AsMemory());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CompressedContent.Should().Be("Compressed: A1: Product");
        result.Statistics.CompressedCellCount.Should().Be(50);
        result.Statistics.CompressionRatio.Should().BeApproximately(0.95, 0.01);
        result.Warnings.Should().BeEmpty();

        _anchorDetectorMock.Verify(x => x.FindWorkbookAnchorsAsync(
            It.IsAny<WorkbookContext>(),
            2,
            It.Is<AnchorDetectionOptions>(opts =>
                opts.MinHeterogeneityScore == 0.6 &&
                opts.ConsiderStyles == false &&
                opts.DetectMultiLevelHeaders == true)), Times.Once);

        _skeletonExtractorMock.Verify(x => x.ExtractWorkbookSkeletonAsync(
            It.IsAny<WorkbookContext>(),
            It.IsAny<WorkbookAnchors>(),
            It.Is<SkeletonExtractionOptions>(opts =>
                opts.PreserveNearbyNonEmpty == true &&
                opts.PreserveFormulas == false &&
                opts.MinCompressionRatio == 0.5)), Times.Once);
    }

    [Fact]
    public async Task Handle_AggressiveCompression_AppliesStrictParameters()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.Aggressive,
            TargetTokenLimit = 500
        };

        var workbook = CreateTestWorkbook();
        var anchors = new WorkbookAnchors();
        var skeleton = new SkeletonWorkbook
        {
            OriginalFilePath = "test.xlsx",
            Sheets =
            [
                new SkeletonSheet
                {
                    OriginalName = "Sheet1",
                    SkeletonCells = new Dictionary<string, EnhancedCellEntity>()
                }
            ],
            GlobalStats = new WorkbookCompressionStats
            {
                OriginalCellCount = 1000,
                SkeletonCellCount = 10,
                CompressionRatio = 0.99
            }
        };
        var serializedContent = "Minimal content";

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _anchorDetectorMock.Setup(x => x.FindWorkbookAnchorsAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<int>(),
                It.IsAny<AnchorDetectionOptions>()))
            .ReturnsAsync(anchors);

        _skeletonExtractorMock.Setup(x => x.ExtractWorkbookSkeletonAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<WorkbookAnchors>(),
                It.IsAny<SkeletonExtractionOptions>()))
            .ReturnsAsync(skeleton);

        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(serializedContent.AsMemory());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CompressedContent.Should().Be("Minimal content");
        result.Statistics.CompressedCellCount.Should().Be(10);
        result.Warnings.Should().Contain("Aggressive compression applied - some context may be lost");

        _anchorDetectorMock.Verify(x => x.FindWorkbookAnchorsAsync(
            It.IsAny<WorkbookContext>(),
            0, // No expansion for aggressive
            It.Is<AnchorDetectionOptions>(opts =>
                opts.MinHeterogeneityScore == 0.8 &&
                opts.ConsiderStyles == false &&
                opts.ConsiderNumberFormats == false &&
                opts.DetectMultiLevelHeaders == false)), Times.Once);

        _skeletonExtractorMock.Verify(x => x.ExtractWorkbookSkeletonAsync(
            It.IsAny<WorkbookContext>(),
            It.IsAny<WorkbookAnchors>(),
            It.Is<SkeletonExtractionOptions>(opts =>
                opts.PreserveNearbyNonEmpty == false &&
                opts.PreserveFormulas == false &&
                opts.PreserveFormattedCells == false &&
                opts.MinCompressionRatio == 0.8)), Times.Once);
    }

    [Fact]
    public async Task Handle_ExceedsTokenLimit_AppliesMoreAggressiveCompression()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.Balanced,
            TargetTokenLimit = 100
        };

        var workbook = CreateTestWorkbook();
        var anchors = new WorkbookAnchors();
        var skeleton = new SkeletonWorkbook
        {
            OriginalFilePath = "test.xlsx",
            Sheets =
            [
                new SkeletonSheet
                {
                    OriginalName = "Sheet1",
                    SkeletonCells = new Dictionary<string, EnhancedCellEntity>()
                }
            ],
            GlobalStats = new WorkbookCompressionStats
            {
                OriginalCellCount = 1000,
                SkeletonCellCount = 20,
                CompressionRatio = 0.98
            }
        };

        // First attempt returns content that's too long
        var longContent = new string('x', 500); // ~125 tokens
        var shortContent = "Short content"; // ~3 tokens

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _anchorDetectorMock.Setup(x => x.FindWorkbookAnchorsAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<int>(),
                It.IsAny<AnchorDetectionOptions>()))
            .ReturnsAsync(anchors);

        _skeletonExtractorMock.Setup(x => x.ExtractWorkbookSkeletonAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<WorkbookAnchors>(),
                It.IsAny<SkeletonExtractionOptions>()))
            .ReturnsAsync(skeleton);

        var serializerCallCount = 0;
        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(() =>
            {
                serializerCallCount++;
                return (serializerCallCount == 1 ? longContent : shortContent).AsMemory();
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.CompressedContent.Should().Be("Short content");
        result.EstimatedTokens.Should().BeLessThan(100);
        result.Warnings.Should().Contain(w => w.Contains("exceeds target limit"));
        result.Warnings.Should().Contain("Aggressive compression applied - some context may be lost");

        // Verify aggressive compression was applied after initial attempt
        _skeletonExtractorMock.Verify(x => x.ExtractWorkbookSkeletonAsync(
            It.IsAny<WorkbookContext>(),
            It.IsAny<WorkbookAnchors>(),
            It.IsAny<SkeletonExtractionOptions>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_LowCompressionRatio_AddsWarning()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.Balanced
        };

        var workbook = CreateTestWorkbook();
        var anchors = new WorkbookAnchors();
        var skeleton = new SkeletonWorkbook
        {
            OriginalFilePath = "test.xlsx",
            Sheets =
            [
                new SkeletonSheet
                {
                    OriginalName = "Sheet1",
                    SkeletonCells = new Dictionary<string, EnhancedCellEntity>()
                }
            ],
            GlobalStats = new WorkbookCompressionStats
            {
                OriginalCellCount = 1000,
                SkeletonCellCount = 800,
                CompressionRatio = 0.2 // Low compression
            }
        };
        var serializedContent = "Poorly compressed content";

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _anchorDetectorMock.Setup(x => x.FindWorkbookAnchorsAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<int>(),
                It.IsAny<AnchorDetectionOptions>()))
            .ReturnsAsync(anchors);

        _skeletonExtractorMock.Setup(x => x.ExtractWorkbookSkeletonAsync(
                It.IsAny<WorkbookContext>(),
                It.IsAny<WorkbookAnchors>(),
                It.IsAny<SkeletonExtractionOptions>()))
            .ReturnsAsync(skeleton);

        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(serializedContent.AsMemory());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Warnings.Should().Contain(w => w.Contains("Low compression ratio achieved"));
    }

    [Fact]
    public async Task Handle_UnsupportedStrategy_ThrowsException()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.Custom // Not implemented
        };

        var workbook = CreateTestWorkbook();

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WorkbookLoadFails_ThrowsException()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "nonexistent.xlsx",
            Strategy = CompressionStrategy.None
        };

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("File not found"));

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_LoadOptionsRespectCommandSettings()
    {
        // Arrange
        var command = new CompressWorkbookCommand
        {
            FilePath = "test.xlsx",
            Strategy = CompressionStrategy.None,
            IncludeFormatting = true,
            IncludeFormulas = false
        };

        var workbook = CreateTestWorkbook();
        var serializedContent = "Content";

        _workbookLoaderMock.Setup(x => x.LoadAsync(
                It.IsAny<string>(),
                It.IsAny<WorkbookLoadOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(workbook);

        _vanillaSerializerMock.Setup(x => x.Serialize(
                It.IsAny<WorkbookContext>(),
                It.IsAny<VanillaSerializationOptions>()))
            .Returns(serializedContent.AsMemory());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _workbookLoaderMock.Verify(x => x.LoadAsync(
            "test.xlsx",
            It.Is<WorkbookLoadOptions>(opts =>
                opts.IncludeStyles == true &&
                opts.IncludeFormulas == false &&
                opts.MemoryOptimization == MemoryOptimizationLevel.Balanced),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static WorkbookContext CreateTestWorkbook()
    {
        return new WorkbookContext
        {
            FilePath = "test.xlsx",
            Worksheets =
            [
                new WorksheetContext
                {
                    Name = "Sheet1",
                    Index = 0,
                    Cells = new Dictionary<string, EnhancedCellEntity>
                    {
                        ["A1"] = new EnhancedCellEntity { Address = "A1", FormattedValue = "Product" },
                        ["B1"] = new EnhancedCellEntity { Address = "B1", FormattedValue = "Sales" },
                        ["A2"] = new EnhancedCellEntity { Address = "A2", FormattedValue = "Product A" },
                        ["B2"] = new EnhancedCellEntity { Address = "B2", FormattedValue = "1000" }
                    },
                    Dimensions = new WorksheetDimensions
                    {
                        TotalCells = 1000,
                        NonEmptyCells = 100
                    }
                }
            ],
            Metadata = new WorkbookMetadata
            {
                FileName = "test.xlsx"
            },
            Statistics = new WorkbookStatistics
            {
                TotalCells = 1000,
                NonEmptyCells = 100
            }
        };
    }
}