using Application.UseCases.Workflow.CreateWorkflow;
using Application.UseCases.Workflow.ListWorkflows;
using Application.UseCases.Workflow.PurchaseWorkflow;
using Application.UseCases.Workflow.GetWorkflowById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkflowsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetWorkflows()
    {
        var workflows = await _mediator.Send(new ListWorkflowsQuery());
        return Ok(workflows);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var workflow = await _mediator.Send(new GetWorkflowByIdQuery(id));
        if (workflow is null) return NotFound();
        return Ok(workflow);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowCommand command)
    {
        var workflow = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, workflow);
    }

    [Authorize]
    [HttpPost("{id}/purchase")]
    public async Task<IActionResult> Purchase(Guid id)
    {
        if (!Guid.TryParse(User.FindFirst("sub")?.Value, out var userId))
        {
            return Unauthorized();
        }

        // The handler now throws exceptions which will be caught by GlobalExceptionMiddleware
        await _mediator.Send(new PurchaseWorkflowCommand(userId, id));
        return Ok(new { message = "Workflow purchased successfully" });
    }
}
