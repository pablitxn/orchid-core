using System;
using System.Threading.Tasks;
using Core.Application.DTOs.KnowledgeBase;
using Core.Application.UseCases.KnowledgeBase.DeleteKnowledgeBase;
using Core.Application.UseCases.KnowledgeBase.GetKnowledgeBaseById;
using Core.Application.UseCases.KnowledgeBase.GetKnowledgeBaseList;
using Core.Application.UseCases.KnowledgeBase.UpdateKnowledgeBase;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IMediator _mediator;

    public KnowledgeBaseController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get paginated list of knowledge base documents
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<KnowledgeBaseListResponseDto>> GetList([FromQuery] KnowledgeBaseQueryDto query)
    {
        var userId = User.GetUserId();
        var result = await _mediator.Send(new GetKnowledgeBaseListQuery(query, userId));
        return Ok(result);
    }

    /// <summary>
    /// Get a specific knowledge base document by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseItemDto>> GetById(Guid id)
    {
        var userId = User.GetUserId();
        var result = await _mediator.Send(new GetKnowledgeBaseByIdQuery(id, userId));
        return Ok(result);
    }

    /// <summary>
    /// Update knowledge base document metadata
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseItemDto>> Update(Guid id, [FromBody] UpdateKnowledgeBaseDto updateDto)
    {
        var userId = User.GetUserId();
        var result = await _mediator.Send(new UpdateKnowledgeBaseCommand(id, userId, updateDto));
        return Ok(result);
    }

    /// <summary>
    /// Delete a knowledge base document
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = User.GetUserId();
        await _mediator.Send(new DeleteKnowledgeBaseCommand(id, userId));
        return NoContent();
    }
}