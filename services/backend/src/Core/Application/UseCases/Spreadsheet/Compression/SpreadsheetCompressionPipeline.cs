using System.Diagnostics;
using System.Text;
using Application.Interfaces.Spreadsheet;
using Application.UseCases.Spreadsheet.Compression.PipelineSteps;
using Domain.ValueObjects.Spreadsheet;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Spreadsheet.Compression;

public sealed class SpreadsheetCompressionPipeline : ISpreadsheetCompressionPipeline
{
    private readonly List<IPipelineStep> _steps;
    private readonly ILogger<SpreadsheetCompressionPipeline>? _logger;

    public SpreadsheetCompressionPipeline(List<IPipelineStep> steps,
        ILogger<SpreadsheetCompressionPipeline>? logger = null)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _logger = logger;
    }

    public async Task<CompressionResult> ExecuteAsync(WorkbookContext workbook,
        CancellationToken cancellationToken = default)
    {
        var overallStopwatch = Stopwatch.StartNew();
        var context = new PipelineContext { OriginalWorkbook = workbook };
        var stepTimings = new Dictionary<string, TimeSpan>();

        // Calculate original token count
        var originalText = GenerateVanillaRepresentation(workbook);
        var originalTokenCount = Encoding.UTF8.GetByteCount(originalText);

        _logger?.LogInformation("Starting compression pipeline with {StepCount} steps", _steps.Count);

        string finalCompressedText = originalText;
        var lastSuccessfulStep = -1;

        for (var i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            _logger?.LogDebug("Executing step {StepIndex}: {StepName}", i + 1, step.Name);

            var result = await step.ExecuteAsync(context, cancellationToken);

            stepTimings[step.Name] = result.ExecutionTime;

            if (!result.Success)
            {
                _logger?.LogError("Step {StepName} failed: {Error}", step.Name, result.Error);
                break;
            }

            // Merge outputs into context
            foreach (var (key, value) in result.Outputs)
            {
                context.IntermediateResults[key] = value;
            }

            // Update compressed text if this step produced one
            if (result.Outputs.TryGetValue("CompressedText", out var compressedTextObj))
            {
                finalCompressedText = compressedTextObj.ToString() ?? finalCompressedText;
            }

            lastSuccessfulStep = i;
            _logger?.LogDebug("Step {StepName} completed in {Duration}ms", step.Name,
                result.ExecutionTime.TotalMilliseconds);
        }

        var finalTokenCount = Encoding.UTF8.GetByteCount(finalCompressedText);
        // Safely compute compression ratio, avoid division by zero
        var ratio = finalTokenCount != 0 ? (double)originalTokenCount / finalTokenCount : 0;

        _logger?.LogInformation(
            "Pipeline completed. Original tokens: {Original}, Compressed tokens: {Compressed}, Ratio: {Ratio:F2}x",
            originalTokenCount,
            finalTokenCount,
            ratio);

        return new CompressionResult
        {
            CompressedText = finalCompressedText,
            OriginalTokenCount = originalTokenCount,
            CompressedTokenCount = finalTokenCount,
            StepTimings = stepTimings,
            Artifacts = context.Artifacts
        };
    }

    private static string GenerateVanillaRepresentation(WorkbookContext workbook)
    {
        var sb = new StringBuilder();

        foreach (var worksheet in workbook.Worksheets)
        {
            sb.AppendLine($"Sheet: {worksheet.Name}");
            
            // First pass: collect all unique formats
            var formats = new Dictionary<string, int>();
            var formatIndex = 0;
            foreach (var cell in worksheet.Cells.Where(c => !string.IsNullOrEmpty(c.NumberFormatString)))
            {
                if (!formats.ContainsKey(cell.NumberFormatString!))
                {
                    formats[cell.NumberFormatString!] = formatIndex++;
                }
            }
            
            // Write format definitions if any
            if (formats.Count > 0)
            {
                sb.AppendLine("Formats:");
                foreach (var (format, idx) in formats)
                {
                    sb.AppendLine($"F{idx}: {format}");
                }
                sb.AppendLine();
            }

            // Write data in a verbose but realistic format
            sb.AppendLine("Data:");
            foreach (var cell in worksheet.Cells.OrderBy(c => c.Address.Row).ThenBy(c => c.Address.Column))
            {
                var value = cell.GetStringValue();
                var formatRef = "";
                if (!string.IsNullOrEmpty(cell.NumberFormatString) && formats.TryGetValue(cell.NumberFormatString, out var fIdx))
                {
                    formatRef = $" [F{fIdx}]";
                }
                
                // Verbose format: full address, value, and format reference
                sb.AppendLine($"Cell {cell.Address.A1Reference}: {value}{formatRef}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}

public sealed class SpreadsheetPipelineBuilder : ISpreadsheetPipelineBuilder
{
    private readonly List<IPipelineStep> _steps = new();

    private readonly IServiceProvider _serviceProvider;

    // Cache logger to avoid repeated service resolution
    private readonly ILogger<SpreadsheetPipelineBuilder>? _builderLogger;

    public SpreadsheetPipelineBuilder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        // Retrieve and cache logger instance
        _builderLogger =
            _serviceProvider.GetService(typeof(ILogger<SpreadsheetPipelineBuilder>)) as
                ILogger<SpreadsheetPipelineBuilder>;
    }


    public ISpreadsheetPipelineBuilder AddStructuralAnchorDetection(int k = 3)
    {
        throw new NotImplementedException();
    }

    public ISpreadsheetPipelineBuilder AddSkeletonExtraction(int k = 3)
    {
        throw new NotImplementedException();
    }

    public ISpreadsheetPipelineBuilder AddInvertedIndex(InvertedIndexOptions? options = null)
    {
        var translator = _serviceProvider.GetService(typeof(IInvertedIndexTranslator)) as IInvertedIndexTranslator
                         ?? new InvertedIndexTranslator();

        _steps.Add(new InvertedIndexStep(translator, options));
        return this;
    }

    public ISpreadsheetPipelineBuilder AddFormatAggregation(FormatAggregationOptions? options = null)
    {
        var aggregator = _serviceProvider.GetService(typeof(IFormatAwareAggregator)) as IFormatAwareAggregator
                         ?? new FormatAwareAggregator();

        _steps.Add(new FormatAggregationStep(aggregator, options));
        return this;
    }

    public ISpreadsheetPipelineBuilder AddStep(IPipelineStep step)
    {
        _steps.Add(step ?? throw new ArgumentNullException(nameof(step)));
        return this;
    }

    public ISpreadsheetCompressionPipeline Build()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("Pipeline must have at least one step");

        var logger =
            _serviceProvider.GetService(typeof(ILogger<SpreadsheetCompressionPipeline>)) as
                ILogger<SpreadsheetCompressionPipeline>;

        return new SpreadsheetCompressionPipeline(new List<IPipelineStep>(_steps), logger);
    }

    private ILogger<SpreadsheetPipelineBuilder>? Logger => _builderLogger;
}