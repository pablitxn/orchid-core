namespace Orchid.Infrastructure.Ai.AgentFramework;

/// <summary>
/// Configuration settings for the model resolution service.
/// </summary>
public sealed class ModelResolutionSettings
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "ModelResolution";

    /// <summary>
    /// System default model ID when no other preference is configured.
    /// Default: gpt-5-nano-2025-08-07.
    /// </summary>
    public string DefaultModel { get; set; } = "gpt-5-nano-2025-08-07";

    /// <summary>
    /// Emergency fallback model if all other resolution fails.
    /// Should be a reliable, always-available model.
    /// Default: gpt-5-nano-2025-08-07.
    /// </summary>
    public string EmergencyFallback { get; set; } = "gpt-5-nano-2025-08-07";

    /// <summary>
    /// Cache duration in minutes for user preference lookups.
    /// Default: 5 minutes.
    /// </summary>
    public int UserPreferenceCacheMinutes { get; set; } = 5;

    /// <summary>
    /// Whether to log detailed resolution information.
    /// Default: false.
    /// </summary>
    public bool EnableDetailedLogging { get; set; }
}
