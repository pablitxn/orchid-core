using Domain.ValueObjects.Spreadsheet;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Creates a JSON inverted index where keys are values/formats and values are cell address lists.
/// Eliminates empty cells and repeated values.
/// </summary>
public interface IInvertedIndexTranslator
{
    /// <summary>
    /// Converts a worksheet to a compact JSON inverted index.
    /// </summary>
    /// <param name="worksheet">The worksheet context to process</param>
    /// <param name="options">Options for index generation</param>
    /// <returns>JSON string with the inverted index</returns>
    Task<string> ToInvertedIndexAsync(WorksheetContext worksheet, InvertedIndexOptions? options = null);
}

public sealed class InvertedIndexOptions
{
    /// <summary>
    /// Whether to optimize contiguous ranges (e.g., A2:A10 instead of A2,A3,A4...)
    /// </summary>
    public bool OptimizeRanges { get; init; } = true;
    
    /// <summary>
    /// Whether to include number format strings as separate entries
    /// </summary>
    public bool IncludeFormats { get; init; } = true;
    
    /// <summary>
    /// Maximum number of addresses before switching to range notation
    /// </summary>
    public int RangeThreshold { get; init; } = 3;
}