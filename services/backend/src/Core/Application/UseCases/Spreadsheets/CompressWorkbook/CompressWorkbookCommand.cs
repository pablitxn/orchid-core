using MediatR;

namespace Application.UseCases.Spreadsheets.CompressWorkbook;

/// <summary>
/// Command to compress a workbook for LLM processing.
/// </summary>
public sealed record CompressWorkbookCommand : IRequest<CompressWorkbookResult>
{
    /// <summary>
    /// Path to the workbook file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Compression strategy to use.
    /// </summary>
    public CompressionStrategy Strategy { get; init; } = CompressionStrategy.None;

    /// <summary>
    /// Target token limit for the compressed output.
    /// </summary>
    public int? TargetTokenLimit { get; init; }

    /// <summary>
    /// Include formatting information in output.
    /// </summary>
    public bool IncludeFormatting { get; init; } = false;

    /// <summary>
    /// Include formulas in output.
    /// </summary>
    public bool IncludeFormulas { get; init; } = true;
}

/// <summary>
/// Compression strategies.
/// </summary>
public enum CompressionStrategy
{
    /// <summary>
    /// No compression, vanilla serialization only.
    /// </summary>
    None,

    /// <summary>
    /// Balanced compression using anchors and skeleton extraction.
    /// </summary>
    Balanced,

    /// <summary>
    /// Aggressive compression, may lose more context.
    /// </summary>
    Aggressive,

    /// <summary>
    /// Custom compression with specific parameters.
    /// </summary>
    Custom
}

/// <summary>
/// Result of workbook compression.
/// </summary>
public sealed record CompressWorkbookResult
{
    /// <summary>
    /// Compressed workbook representation.
    /// </summary>
    public string CompressedContent { get; init; } = string.Empty;

    /// <summary>
    /// Estimated token count.
    /// </summary>
    public int EstimatedTokens { get; init; }

    /// <summary>
    /// Compression statistics.
    /// </summary>
    public CompressionStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Warnings or issues encountered during compression.
    /// </summary>
    public List<string> Warnings { get; init; } = new();

    public bool Success { get; init; }
}

/// <summary>
/// Compression statistics.
/// </summary>
public sealed record CompressionStatistics
{
    public int OriginalCellCount { get; init; }
    public int CompressedCellCount { get; init; }
    public double CompressionRatio { get; init; }
    public int SheetsProcessed { get; init; }
    public long ProcessingTimeMs { get; init; }
    public long MemoryUsedBytes { get; init; }
}