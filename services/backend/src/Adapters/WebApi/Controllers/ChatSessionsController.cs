using Application.UseCases.ChatSession.ArchiveChatSession;
using Application.UseCases.ChatSession.CreateChatSession;
using Application.UseCases.ChatSession.DeleteChatSessions;
using Application.UseCases.ChatSession.GetChatSessionById;
using Application.UseCases.ChatSession.ListChatSessions;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/chat-sessions")]
public class ChatSessionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid userId,
        [FromQuery] bool archived = false,
        [FromQuery] Guid? agentId = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] InteractionType? type = null)
    {
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
}

public record ArchiveChatSessionRequest(bool Archived);