using System.Security.Claims;
using Application.UseCases.ChatV2.Common;
using Application.UseCases.ChatV2.ListMessages;
using Application.UseCases.ChatV2.SendMessage;
using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models.V2;

namespace WebApi.Controllers.V2;

/// <summary>
/// V2 Chat Messages API with support for artifacts, tool_calls, reactions,
/// and cursor-based pagination.
/// </summary>
[Authorize]
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/chat-sessions/{sessionId}/messages")]
public class ChatMessagesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Extracts the authenticated user's ID from JWT claims.
    /// </summary>
    private Guid GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID claim not found or invalid in token.");
        }

        return userId;
    }

    /// <summary>
    /// Lists messages for a chat session with cursor-based pagination.
    /// </summary>
    /// <param name="sessionId">The session ID to retrieve messages for.</param>
    /// <param name="limit">Maximum number of messages to return (default: 50, max: 100).</param>
    /// <param name="cursor">Base64-encoded cursor for pagination.</param>
    /// <param name="direction">"backward" for older messages, "forward" for newer.</param>
    /// <returns>Paginated list of messages with cursor information.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedMessagesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListMessages(
        string sessionId,
        [FromQuery] int limit = 50,
        [FromQuery] string? cursor = null,
        [FromQuery] string direction = "backward")
    {
        var userId = GetAuthenticatedUserId();

        var result = await _mediator.Send(new ListMessagesV2Query(
            SessionId: sessionId,
            UserId: userId,
            Limit: limit,
            Cursor: cursor,
            Direction: direction));

        return Ok(result);
    }

    /// <summary>
    /// Sends a new message in a chat session.
    /// </summary>
    /// <param name="sessionId">The session ID to send the message to.</param>
    /// <param name="request">The message content and optional artifacts.</param>
    /// <returns>The created message.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MessageV2Dto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendMessage(
        string sessionId,
        [FromBody] SendMessageV2Request request)
    {
        var userId = GetAuthenticatedUserId();

        var result = await _mediator.Send(new SendMessageV2Command(
            SessionId: sessionId,
            UserId: userId,
            Content: request.Content,
            Artifacts: request.Artifacts));

        return CreatedAtAction(
            nameof(ListMessages),
            new { sessionId },
            result);
    }
}
