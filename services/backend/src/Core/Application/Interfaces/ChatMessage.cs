namespace Application.Interfaces;

/// <summary>
///     Represents a single item in a chat conversation. In addition to the
///     standard OpenAI roles (<c>user</c>, <c>assistant</c>, <c>system</c>),
///     the message may capture tool calls/responses or custom activities.
///     Only messages with the three standard roles will be sent to the LLM.
/// </summary>
/// <param name="Role">Message role such as "user", "assistant", "system" or "activity".</param>
/// <param name="Content">Free form text content.</param>
/// <param name="ActivityType">Optional activity identifier when <paramref name="Role" /> is "activity".</param>
/// <param name="ActivityPayload">Optional payload for the activity.</param>
/// <param name="ToolCalls">OpenAI tool call objects.</param>
/// <param name="ToolResponses">OpenAI tool response objects.</param>
/// <param name="Metadata">Optional metadata dictionary for tracking additional information like session IDs.</param>
public sealed record ChatMessage(
    string Role,
    string Content,
    string? ActivityType = null,
    object? ActivityPayload = null,
    object? ToolCalls = null,
    object? ToolResponses = null,
    Dictionary<string, object>? Metadata = null);