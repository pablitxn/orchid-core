namespace Domain.Entities;

/// <summary>
/// Represents a chat message in the V2 format with support for tool calls,
/// artifacts, and reactions. Uses JSONB columns for complex nested data.
/// </summary>
public class MessageV2Entity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The string-based session identifier (matches ChatSessionEntity.SessionId).
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Foreign key to the ChatSessionEntity.
    /// </summary>
    public Guid ChatSessionId { get; set; }

    /// <summary>
    /// Navigation property to the parent chat session.
    /// </summary>
    public ChatSessionEntity? ChatSession { get; set; }

    /// <summary>
    /// Message role: 'user', 'assistant', or 'system'.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Text content of the message. Can be null when message contains only tool_calls.
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Serialized JSON array of tool calls (OpenAI format).
    /// </summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>
    /// Serialized JSON array of artifacts (code snippets, files, images).
    /// </summary>
    public string? ArtifactsJson { get; set; }

    /// <summary>
    /// Serialized JSON array of user reactions (emoji reactions).
    /// </summary>
    public string? ReactionsJson { get; set; }

    /// <summary>
    /// Unix timestamp in milliseconds (matches frontend interface).
    /// </summary>
    public long Timestamp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to artifacts attached to this message.
    /// </summary>
    public ICollection<MessageArtifactEntity> Artifacts { get; set; } = new List<MessageArtifactEntity>();
}
