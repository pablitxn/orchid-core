namespace Application.UseCases.ChatV2.Common;

/// <summary>
/// Response DTO for paginated message queries with cursor-based navigation.
/// </summary>
public sealed record PaginatedMessagesDto
{
    /// <summary>
    /// The list of messages in this page.
    /// </summary>
    public required List<MessageV2Dto> Messages { get; init; }

    /// <summary>
    /// Cursor to fetch the next page (older messages when direction is backward).
    /// Null if no more pages exist.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>
    /// Cursor to fetch the previous page (newer messages when direction is backward).
    /// Null if at the beginning.
    /// </summary>
    public string? PreviousCursor { get; init; }

    /// <summary>
    /// Indicates whether more messages exist beyond the current page.
    /// </summary>
    public required bool HasMore { get; init; }

    /// <summary>
    /// Total count of messages in the session.
    /// </summary>
    public required int TotalCount { get; init; }
}

/// <summary>
/// Internal representation of a cursor for pagination.
/// Encoded as base64 when sent to/from the client.
/// </summary>
public sealed record CursorDto
{
    /// <summary>
    /// Unix timestamp in milliseconds for cursor position.
    /// </summary>
    public required long Timestamp { get; init; }

    /// <summary>
    /// Message ID for tie-breaking when multiple messages have the same timestamp.
    /// </summary>
    public required string MessageId { get; init; }
}
