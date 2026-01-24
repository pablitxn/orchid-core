using Application.UseCases.ChatV2.Common;
using MediatR;

namespace Application.UseCases.ChatV2.ListMessages;

/// <summary>
/// Query to list messages for a chat session with cursor-based pagination.
/// </summary>
/// <param name="SessionId">The session ID to retrieve messages for.</param>
/// <param name="UserId">The authenticated user's ID for authorization.</param>
/// <param name="Limit">Maximum number of messages to return (default: 50, max: 100).</param>
/// <param name="Cursor">Base64-encoded cursor for pagination (optional).</param>
/// <param name="Direction">"backward" for older messages, "forward" for newer messages.</param>
public sealed record ListMessagesV2Query(
    string SessionId,
    Guid UserId,
    int Limit = 50,
    string? Cursor = null,
    string Direction = "backward"
) : IRequest<PaginatedMessagesDto>;
