using System.Threading;
using Domain.ValueObjects.Spreadsheet;

namespace Application.Interfaces.Spreadsheet;

/// <summary>
/// Aggregates cells with identical number format strings or applies type-guessing rules.
/// Replaces adjacent regions with type tokens.
/// </summary>
public interface IFormatAwareAggregator
{
    /// <summary>
    /// Aggregates cells based on format awareness and type recognition.
    /// </summary>
    /// <param name="worksheet">The worksheet to aggregate</param>
    /// <param name="options">Aggregation options</param>
    /// <returns>Aggregated result with type tokens</returns>
    Task<AggregatedWorksheet> AggregateAsync(WorksheetContext worksheet, FormatAggregationOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class FormatAggregationOptions
{
    /// <summary>
    /// Enable type recognition for cells without explicit format strings
    /// </summary>
    public bool EnableTypeRecognition { get; init; } = true;
    
    /// <summary>
    /// Custom type recognizers to use
    /// </summary>
    public IList<ITypeRecognizer> TypeRecognizers { get; init; } = new List<ITypeRecognizer>();
    
    /// <summary>
    /// Minimum cells in a group to perform aggregation
    /// </summary>
    public int MinGroupSize { get; init; } = 2;
}

public interface ITypeRecognizer
{
    string TypeName { get; }
    bool CanRecognize(object? value, string? formatString);
    string GetTypeToken();
}

public sealed class AggregatedWorksheet
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyList<AggregatedRegion> Regions { get; init; } = Array.Empty<AggregatedRegion>();
    public double CompressionRatio { get; init; }
}

public sealed class AggregatedRegion
{
    public CellAddress StartAddress { get; init; }
    public CellAddress EndAddress { get; init; }
    public string TypeToken { get; init; } = string.Empty;
    public string? FormatString { get; init; }
    public int CellCount { get; init; }
}