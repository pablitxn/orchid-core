using System.Security.Claims;
using Application.UseCases.ChatSession.ArchiveChatSession;
using Application.UseCases.ChatSession.ChangeSessionModel;
using Application.UseCases.ChatSession.CreateChatSession;
using Application.UseCases.ChatSession.DeleteChatSessions;
using Application.UseCases.ChatSession.GetChatSessionById;
using Application.UseCases.ChatSession.ListChatSessions;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/chat-sessions")]
public class ChatSessionsController(IMediator mediator) : ControllerBase
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

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] bool archived = false,
        [FromQuery] Guid? agentId = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] InteractionType? type = null)
    {
        // Security: Use authenticated user ID from JWT instead of query parameter to prevent IDOR
        var userId = GetAuthenticatedUserId();
        var result = await _mediator.Send(
            new ListChatSessionsQuery(userId, archived, agentId, teamId, startDate, endDate, type));
        return Ok(result);
    }

    [HttpGet("{sessionId}")]
    public async Task<IActionResult> GetBySessionId(string sessionId)
    {
        var result = await _mediator.Send(new GetChatSessionByIdQuery(sessionId));
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateChatSessionCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPut("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] ArchiveChatSessionRequest request)
    {
        await _mediator.Send(new ArchiveChatSessionCommand(id, request.Archived));
        return NoContent();
    }

    [HttpPost("delete")]
    public async Task<IActionResult> Delete([FromBody] DeleteChatSessionsCommand command)
    {
        await _mediator.Send(command);
        return NoContent();
    }

    /// <summary>
    /// Changes the active AI model for a chat session.
    /// </summary>
    /// <param name="sessionId">The session ID (string identifier).</param>
    /// <param name="request">The model change request.</param>
    [HttpPost("{sessionId}/model")]
    [ProducesResponseType(typeof(ChangeSessionModelDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeModel(
        string sessionId,
        [FromBody] ChangeModelRequest request)
    {
        var userId = GetAuthenticatedUserId();

        var result = await _mediator.Send(
            new ChangeSessionModelCommand(sessionId, userId, request.ModelId));

        return Ok(result);
    }
}

public record ArchiveChatSessionRequest(bool Archived);
public record ChangeModelRequest(string ModelId);