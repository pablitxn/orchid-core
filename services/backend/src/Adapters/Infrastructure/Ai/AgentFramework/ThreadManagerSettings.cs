namespace Orchid.Infrastructure.Ai.AgentFramework;

/// <summary>
/// Configuration settings for the AgentThreadManager and cleanup service.
/// </summary>
public sealed class ThreadManagerSettings
{
    /// <summary>
    /// Configuration section name in appsettings.
    /// </summary>
    public const string SectionName = "ThreadManager";

    /// <summary>
    /// Time in minutes after which an idle thread is considered expired.
    /// Default: 60 minutes (1 hour).
    /// </summary>
    public int ThreadIdleTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Interval in seconds between cleanup runs.
    /// Default: 300 seconds (5 minutes).
    /// </summary>
    public int CleanupIntervalSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to persist thread history to Redis before cleanup.
    /// Default: true.
    /// </summary>
    public bool PersistToRedisOnCleanup { get; set; } = true;
}
