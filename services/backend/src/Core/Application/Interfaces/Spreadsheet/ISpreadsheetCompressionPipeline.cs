using Domain.ValueObjects.Spreadsheet;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Configurable pipeline for spreadsheet compression with fluent builder pattern.
/// </summary>
public interface ISpreadsheetCompressionPipeline
{
    /// <summary>
    /// Executes the configured pipeline on a workbook context.
    /// </summary>
    Task<CompressionResult> ExecuteAsync(WorkbookContext workbook, CancellationToken cancellationToken = default);
}

public interface ISpreadsheetPipelineBuilder
{
    /// <summary>
    /// Adds structural anchor detection step.
    /// </summary>
    ISpreadsheetPipelineBuilder AddStructuralAnchorDetection(int k = 3);
    
    /// <summary>
    /// Adds skeleton extraction step.
    /// </summary>
    ISpreadsheetPipelineBuilder AddSkeletonExtraction(int k = 3);
    
    /// <summary>
    /// Adds inverted index translation step.
    /// </summary>
    ISpreadsheetPipelineBuilder AddInvertedIndex(InvertedIndexOptions? options = null);
    
    /// <summary>
    /// Adds format-aware aggregation step.
    /// </summary>
    ISpreadsheetPipelineBuilder AddFormatAggregation(FormatAggregationOptions? options = null);
    
    /// <summary>
    /// Adds a custom pipeline step.
    /// </summary>
    ISpreadsheetPipelineBuilder AddStep(IPipelineStep step);
    
    /// <summary>
    /// Builds the configured pipeline.
    /// </summary>
    ISpreadsheetCompressionPipeline Build();
}

public interface IPipelineStep
{
    string Name { get; }
    Task<PipelineStepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken = default);
}

public sealed class PipelineContext
{
    public WorkbookContext OriginalWorkbook { get; init; } = null!;
    public Dictionary<string, object> IntermediateResults { get; } = new();
    public List<PipelineArtifact> Artifacts { get; } = new();
}

public sealed class PipelineStepResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public Dictionary<string, object> Outputs { get; init; } = new();
    public TimeSpan ExecutionTime { get; init; }
}

public sealed class PipelineArtifact
{
    public string Name { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public byte[] Data { get; init; } = Array.Empty<byte>();
}

public sealed class CompressionResult
{
    public string CompressedText { get; init; } = string.Empty;
    public int OriginalTokenCount { get; init; }
    public int CompressedTokenCount { get; init; }
    public double CompressionRatio => CompressedTokenCount > 0 ? (double)OriginalTokenCount / CompressedTokenCount : 0;
    public Dictionary<string, TimeSpan> StepTimings { get; init; } = new();
    public List<PipelineArtifact> Artifacts { get; init; } = new();
}