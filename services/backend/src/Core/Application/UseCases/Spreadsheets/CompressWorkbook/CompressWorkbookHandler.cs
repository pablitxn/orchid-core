using System.Diagnostics;
using Application.Interfaces;
using Domain.Entities.Spreadsheet;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Spreadsheets.CompressWorkbook;

/// <summary>
/// Handler for workbook compression command.
/// </summary>
public sealed class CompressWorkbookHandler(
    IEnhancedWorkbookLoader workbookLoader,
    IVanillaSerializer vanillaSerializer,
    IStructuralAnchorDetector anchorDetector,
    ISkeletonExtractor skeletonExtractor,
    ILogger<CompressWorkbookHandler> logger)
    : IRequestHandler<CompressWorkbookCommand, CompressWorkbookResult>
{
    private readonly IEnhancedWorkbookLoader _workbookLoader =
        workbookLoader ?? throw new ArgumentNullException(nameof(workbookLoader));

    private readonly IVanillaSerializer _vanillaSerializer =
        vanillaSerializer ?? throw new ArgumentNullException(nameof(vanillaSerializer));

    private readonly IStructuralAnchorDetector _anchorDetector =
        anchorDetector ?? throw new ArgumentNullException(nameof(anchorDetector));

    private readonly ISkeletonExtractor _skeletonExtractor =
        skeletonExtractor ?? throw new ArgumentNullException(nameof(skeletonExtractor));

    private readonly ILogger<CompressWorkbookHandler> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<CompressWorkbookResult> Handle(CompressWorkbookCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var warnings = new List<string>();

        _logger.LogInformation(
            "Starting workbook compression for {FilePath} with strategy {Strategy}",
            request.FilePath,
            request.Strategy);

        try
        {
            // Step 1: Load workbook with metadata
            var loadOptions = new WorkbookLoadOptions
            {
                IncludeStyles = request.IncludeFormatting,
                IncludeFormulas = request.IncludeFormulas,
                MemoryOptimization = MemoryOptimizationLevel.Balanced
            };

            var workbook = await _workbookLoader.LoadAsync(request.FilePath, loadOptions, cancellationToken);

            _logger.LogDebug(
                "Loaded workbook with {SheetCount} sheets and {CellCount} cells",
                workbook.Worksheets.Count,
                workbook.Statistics.TotalCells);

            // Step 2: Apply compression strategy
            string compressedContent;
            int compressedCellCount;

            switch (request.Strategy)
            {
                case CompressionStrategy.None:
                    compressedContent = await ApplyNoCompression(workbook, request, cancellationToken);
                    compressedCellCount = workbook.Statistics.NonEmptyCells;
                    break;

                case CompressionStrategy.Balanced:
                    var balancedResult = await ApplyBalancedCompression(workbook, request, warnings, cancellationToken);
                    compressedContent = balancedResult.content;
                    compressedCellCount = balancedResult.cellCount;
                    break;

                case CompressionStrategy.Aggressive:
                    var aggressiveResult =
                        await ApplyAggressiveCompression(workbook, request, warnings, cancellationToken);
                    compressedContent = aggressiveResult.content;
                    compressedCellCount = aggressiveResult.cellCount;
                    break;

                default:
                    throw new NotSupportedException($"Compression strategy {request.Strategy} is not supported");
            }

            // Step 3: Check token limit
            var estimatedTokens = EstimateTokens(compressedContent);

            if (request.TargetTokenLimit.HasValue && estimatedTokens > request.TargetTokenLimit.Value)
            {
                warnings.Add(
                    $"Compressed content ({estimatedTokens} tokens) exceeds target limit ({request.TargetTokenLimit.Value} tokens)");

                // Try more aggressive compression if needed
                if (request.Strategy != CompressionStrategy.Aggressive)
                {
                    _logger.LogWarning("Applying aggressive compression to meet token limit");
                    var aggressiveResult =
                        await ApplyAggressiveCompression(workbook, request, warnings, cancellationToken);
                    compressedContent = aggressiveResult.content;
                    compressedCellCount = aggressiveResult.cellCount;
                    estimatedTokens = EstimateTokens(compressedContent);
                }
            }

            stopwatch.Stop();

            // Build result
            var statistics = new CompressionStatistics
            {
                OriginalCellCount = workbook.Statistics.TotalCells,
                CompressedCellCount = compressedCellCount,
                CompressionRatio = workbook.Statistics.TotalCells > 0
                    ? 1.0 - (compressedCellCount / (double)workbook.Statistics.TotalCells)
                    : 0,
                SheetsProcessed = workbook.Worksheets.Count,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                MemoryUsedBytes = GC.GetTotalMemory(false)
            };

            _logger.LogInformation(
                "Completed workbook compression: {OriginalCells} → {CompressedCells} cells ({CompressionRatio:P} compression) in {Time}ms",
                statistics.OriginalCellCount,
                statistics.CompressedCellCount,
                statistics.CompressionRatio,
                statistics.ProcessingTimeMs);

            return new CompressWorkbookResult
            {
                CompressedContent = compressedContent,
                EstimatedTokens = estimatedTokens,
                Statistics = statistics,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to compress workbook {FilePath}", request.FilePath);
            throw;
        }
    }

    private Task<string> ApplyNoCompression(
        WorkbookContext workbook,
        CompressWorkbookCommand request,
        CancellationToken cancellationToken)
    {
        var options = new VanillaSerializationOptions
        {
            IncludeEmptyCells = false,
            IncludeNumberFormats = request.IncludeFormatting,
            IncludeFormulas = request.IncludeFormulas,
            IncludeStyles = request.IncludeFormatting
        };

        var serialized = _vanillaSerializer.Serialize(workbook, options);
        return Task.FromResult(serialized.ToString());
    }

    private async Task<(string content, int cellCount)> ApplyBalancedCompression(
        WorkbookContext workbook,
        CompressWorkbookCommand request,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        // Detect anchors with balanced parameters
        var anchorOptions = new AnchorDetectionOptions
        {
            MinHeterogeneityScore = 0.6,
            ConsiderStyles = request.IncludeFormatting,
            ConsiderNumberFormats = request.IncludeFormatting,
            DetectMultiLevelHeaders = true
        };

        var anchors = await _anchorDetector.FindWorkbookAnchorsAsync(workbook, k: 2, anchorOptions);

        // Extract skeleton
        var skeletonOptions = new SkeletonExtractionOptions
        {
            PreserveNearbyNonEmpty = true,
            PreserveFormulas = request.IncludeFormulas,
            PreserveFormattedCells = request.IncludeFormatting,
            MinCompressionRatio = 0.5
        };

        var skeleton = await _skeletonExtractor.ExtractWorkbookSkeletonAsync(workbook, anchors, skeletonOptions);

        // Check compression effectiveness
        if (skeleton.GlobalStats.CompressionRatio < 0.3)
        {
            warnings.Add($"Low compression ratio achieved: {skeleton.GlobalStats.CompressionRatio:P}");
        }

        // Serialize skeleton
        var skeletonWorkbook = ConvertSkeletonToWorkbookContext(skeleton);
        var serializeOptions = new VanillaSerializationOptions
        {
            IncludeEmptyCells = false,
            IncludeNumberFormats = request.IncludeFormatting,
            IncludeFormulas = request.IncludeFormulas,
            IncludeStyles = request.IncludeFormatting
        };

        var serialized = _vanillaSerializer.Serialize(skeletonWorkbook, serializeOptions);
        return (serialized.ToString(), skeleton.GlobalStats.SkeletonCellCount);
    }

    private async Task<(string content, int cellCount)> ApplyAggressiveCompression(
        WorkbookContext workbook,
        CompressWorkbookCommand request,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        // Detect anchors with aggressive parameters
        var anchorOptions = new AnchorDetectionOptions
        {
            MinHeterogeneityScore = 0.8, // Higher threshold
            ConsiderStyles = false, // Ignore styles
            ConsiderNumberFormats = false,
            DetectMultiLevelHeaders = false // Simple headers only
        };

        var anchors = await _anchorDetector.FindWorkbookAnchorsAsync(workbook, k: 0, anchorOptions); // No expansion

        // Extract skeleton aggressively
        var skeletonOptions = new SkeletonExtractionOptions
        {
            PreserveNearbyNonEmpty = false,
            PreserveFormulas = false, // Drop formulas
            PreserveFormattedCells = false,
            MinCompressionRatio = 0.8 // Target high compression
        };

        var skeleton = await _skeletonExtractor.ExtractWorkbookSkeletonAsync(workbook, anchors, skeletonOptions);

        warnings.Add("Aggressive compression applied - some context may be lost");

        // Serialize with minimal options
        var skeletonWorkbook = ConvertSkeletonToWorkbookContext(skeleton);
        var serializeOptions = new VanillaSerializationOptions
        {
            IncludeEmptyCells = false,
            IncludeNumberFormats = false,
            IncludeFormulas = false,
            IncludeStyles = false,
            MaxCells = request.TargetTokenLimit.HasValue ? request.TargetTokenLimit.Value / 4 : null // Rough estimate
        };

        var serialized = _vanillaSerializer.Serialize(skeletonWorkbook, serializeOptions);
        return (serialized.ToString(), skeleton.GlobalStats.SkeletonCellCount);
    }

    private static WorkbookContext ConvertSkeletonToWorkbookContext(SkeletonWorkbook skeleton)
    {
        var worksheets = skeleton.Sheets.Select(sheet => new WorksheetContext
        {
            Name = sheet.OriginalName,
            Index = 0,
            Cells = sheet.SkeletonCells,
            Dimensions = new WorksheetDimensions
            {
                TotalCells = sheet.SkeletonCells.Count,
                NonEmptyCells = sheet.SkeletonCells.Count(c => c.Value.DataType != CellDataType.Empty)
            }
        }).ToList();

        return new WorkbookContext
        {
            FilePath = skeleton.OriginalFilePath,
            Worksheets = worksheets,
            Metadata = new WorkbookMetadata { FileName = Path.GetFileName(skeleton.OriginalFilePath) },
            Statistics = new WorkbookStatistics
            {
                TotalCells = skeleton.GlobalStats.SkeletonCellCount,
                NonEmptyCells = skeleton.GlobalStats.SkeletonCellCount
            }
        };
    }

    private static int EstimateTokens(string content)
    {
        // Simple estimation: ~4 characters per token
        return content.Length / 4;
    }
}