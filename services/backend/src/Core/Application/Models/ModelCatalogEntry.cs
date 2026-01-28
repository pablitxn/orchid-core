using Domain.Enums;

namespace Application.Models;

/// <summary>
/// Represents a model in the available models catalog.
/// </summary>
public sealed record ModelCatalogEntry
{
    /// <summary>
    /// Unique identifier for the model (e.g., "gpt-5-nano-2025-08-07").
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Provider of the model (e.g., "openai", "anthropic").
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Pricing tier for preference-based selection.
    /// </summary>
    public required ModelPreference PricingTier { get; init; }

    /// <summary>
    /// Maximum context window in tokens.
    /// </summary>
    public required int ContextWindow { get; init; }

    /// <summary>
    /// Whether the model supports function/tool calling.
    /// </summary>
    public bool SupportsFunctionCalling { get; init; } = true;

    /// <summary>
    /// Whether the model supports vision/image inputs.
    /// </summary>
    public bool SupportsVision { get; init; }

    /// <summary>
    /// Relative cost multiplier (1.0 = baseline).
    /// </summary>
    public decimal CostMultiplier { get; init; } = 1.0m;

    /// <summary>
    /// Whether this model is currently available for use.
    /// </summary>
    public bool IsActive { get; init; } = true;
}
