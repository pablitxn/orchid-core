using Domain.Enums;

namespace Application.Models;

/// <summary>
/// Result of model resolution containing the resolved model and its source.
/// </summary>
public sealed record ModelResolutionResult
{
    /// <summary>
    /// The resolved model ID to use for AI completions.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// The source in the hierarchy that provided this model.
    /// </summary>
    public required ModelResolutionSource Source { get; init; }

    /// <summary>
    /// Optional metadata about the resolution (e.g., preference that led to selection).
    /// </summary>
    public ModelPreference? AppliedPreference { get; init; }

    /// <summary>
    /// Indicates if this model is a degraded fallback from the user's preferred model.
    /// </summary>
    public bool IsDegraded { get; init; }

    /// <summary>
    /// The original preferred model ID if degradation occurred.
    /// </summary>
    public string? OriginalModelId { get; init; }

    /// <summary>
    /// Reason for degradation if applicable.
    /// </summary>
    public ModelDegradationReason? DegradationReason { get; init; }

    /// <summary>
    /// Estimated credit cost for this model with the current request.
    /// </summary>
    public int? EstimatedCreditCost { get; init; }
}
