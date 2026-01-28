namespace Application.Models;

/// <summary>
/// Context for model resolution containing all relevant information for the hierarchy.
/// </summary>
public sealed record ModelResolutionContext
{
    /// <summary>
    /// Optional agent ID for agent-specific resolution.
    /// </summary>
    public Guid? AgentId { get; init; }

    /// <summary>
    /// Optional user ID for user preference lookup.
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Optional model override from the request. Highest priority if provided.
    /// </summary>
    public string? RequestOverride { get; init; }

    /// <summary>
    /// Available credits for the user. Used for credit-aware fallback resolution.
    /// </summary>
    public int? AvailableCredits { get; init; }

    /// <summary>
    /// Estimated token count for the request. Used for cost calculation.
    /// </summary>
    public int? EstimatedTokens { get; init; }

    /// <summary>
    /// Whether auto-degradation is allowed for this resolution.
    /// When false, will not downgrade to cheaper models on insufficient credits.
    /// </summary>
    public bool AllowAutoDegradation { get; init; } = true;

    /// <summary>
    /// Models to exclude from resolution (e.g., circuit is open).
    /// </summary>
    public IReadOnlySet<string>? ExcludedModels { get; init; }

    /// <summary>
    /// Creates a context for agent-based resolution.
    /// </summary>
    public static ModelResolutionContext ForAgent(Guid agentId, Guid? userId = null, string? requestOverride = null)
        => new() { AgentId = agentId, UserId = userId, RequestOverride = requestOverride };

    /// <summary>
    /// Creates a context for user-based resolution (no agent).
    /// </summary>
    public static ModelResolutionContext ForUser(Guid userId, string? requestOverride = null)
        => new() { UserId = userId, RequestOverride = requestOverride };

    /// <summary>
    /// Creates a context for system default resolution only.
    /// </summary>
    public static ModelResolutionContext Default()
        => new();
}
