namespace Domain.Entities;

/// <summary>
/// Raw plugin usage events for analytics.
/// Each row represents a single event (install, uninstall, invocation, error, review).
/// </summary>
public class PluginUsageEventEntity
{
    public Guid Id { get; set; }

    public Guid PluginId { get; set; }
    public PluginEntity? Plugin { get; set; }

    public Guid? UserId { get; set; }
    public UserEntity? User { get; set; }

    public Guid? AgentId { get; set; }
    public AgentEntity? Agent { get; set; }

    /// <summary>
    /// Event type: "installed", "uninstalled", "invoked", "error", "review_created"
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// JSON metadata specific to event type (function name, parameters, etc.)
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// For error events: the error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// For invocation events: execution duration in milliseconds
    /// </summary>
    public long? DurationMs { get; set; }

    /// <summary>
    /// Timestamp of the event
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
