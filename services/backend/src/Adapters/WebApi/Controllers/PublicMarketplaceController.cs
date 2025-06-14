using Application.UseCases.Plugin.ListAvailablePlugins;
using Application.UseCases.Workflow.ListWorkflows;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/public/marketplace")]
[AllowAnonymous]
public class PublicMarketplaceController(IMediator mediator) : ControllerBase
{
    [HttpGet("plugins")]
    public async Task<IActionResult> ListPlugins(CancellationToken cancellationToken)
    {
        var query = new ListPublicPluginsQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workflows")]
    public async Task<IActionResult> ListWorkflows(CancellationToken cancellationToken)
    {
        var query = new ListPublicWorkflowsQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("plugins/{pluginId}")]
    public async Task<IActionResult> GetPluginDetails(Guid pluginId, CancellationToken cancellationToken)
    {
        var query = new GetPublicPluginDetailsQuery(pluginId);
        var result = await mediator.Send(query, cancellationToken);
        
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("workflows/{workflowId}")]
    public async Task<IActionResult> GetWorkflowDetails(Guid workflowId, CancellationToken cancellationToken)
    {
        var query = new GetPublicWorkflowDetailsQuery(workflowId);
        var result = await mediator.Send(query, cancellationToken);
        
        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}