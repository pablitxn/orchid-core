namespace Application.UseCases.ChatV2.Common;

/// <summary>
/// V2 message DTO matching the frontend interface structure.
/// </summary>
public sealed record MessageV2Dto
{
    public required string Id { get; init; }

    /// <summary>
    /// Role of the message sender: 'user', 'assistant', or 'system'.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    /// Text content of the message. Can be null when message contains only tool_calls.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// List of tool calls made by the assistant.
    /// </summary>
    public List<ToolCallDto>? ToolCalls { get; init; }

    /// <summary>
    /// List of artifacts attached to the message (code, files, images).
    /// </summary>
    public List<ArtifactDto>? Artifacts { get; init; }

    /// <summary>
    /// List of user reactions to this message.
    /// </summary>
    public List<ReactionDto>? Reactions { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds.
    /// </summary>
    public required long Timestamp { get; init; }
}

/// <summary>
/// Represents a tool call made by the assistant (OpenAI format).
/// </summary>
public sealed record ToolCallDto
{
    public required string Id { get; init; }

    /// <summary>
    /// Type of the tool call, typically "function".
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// The function call details.
    /// </summary>
    public required FunctionCallDto Function { get; init; }
}

/// <summary>
/// Represents the function details within a tool call.
/// </summary>
public sealed record FunctionCallDto
{
    /// <summary>
    /// Name of the function being called.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// JSON string containing the function arguments.
    /// </summary>
    public required string Arguments { get; init; }
}

/// <summary>
/// Represents an artifact attached to a message.
/// </summary>
public sealed record ArtifactDto
{
    public required string Id { get; init; }

    /// <summary>
    /// Type of artifact: 'code', 'file', 'image', etc.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Display title for the artifact.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The artifact content (code, base64 data, etc.).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Programming language for code artifacts.
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// MIME type for file/image artifacts.
    /// </summary>
    public string? MimeType { get; init; }
}

/// <summary>
/// Represents a user reaction to a message.
/// </summary>
public sealed record ReactionDto
{
    /// <summary>
    /// ID of the user who reacted.
    /// </summary>
    public required string UserId { get; init; }

    /// <summary>
    /// The emoji reaction.
    /// </summary>
    public required string Emoji { get; init; }

    /// <summary>
    /// Unix timestamp in milliseconds when the reaction was added.
    /// </summary>
    public required long Timestamp { get; init; }
}
