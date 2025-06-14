using Application.UseCases.Plugin.ListAvailablePlugins;
using Application.UseCases.Plugin.ListPlugins;
using Application.UseCases.Plugin.PurchasePlugin;
using Application.UseCases.Plugin.DeletePlugin;
using Application.UseCases.Agent.AddPluginToAgent;
using Application.UseCases.Agent.RemovePluginFromAgent;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PluginController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListPlugins(CancellationToken cancellationToken)
    {
        var query = new ListPluginsQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("available")]
    public async Task<IActionResult> ListAvailablePlugins(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var query = new ListAvailablePluginsQuery(userId);
        var result = await mediator.Send(query, cancellationToken);
        // Return plugins in a property matching the front-end expectation
        return Ok(new { availablePlugins = result.Plugins });
    }

    [HttpPost("{pluginId}/purchase")]
    public async Task<IActionResult> PurchasePlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new PurchasePluginCommand(userId, pluginId);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin purchased successfully" });
    }

    [HttpPost("agent/{agentId}/plugin/{pluginId}")]
    public async Task<IActionResult> AddPluginToAgent(Guid agentId, Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new AddPluginToAgentCommand(userId, agentId, pluginId);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin added to agent successfully" });
    }

    [HttpDelete("agent/{agentId}/plugin/{pluginId}")]
    public async Task<IActionResult> RemovePluginFromAgent(Guid agentId, Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new RemovePluginFromAgentCommand(userId, agentId, pluginId);
        var result = await mediator.Send(command, cancellationToken);
        
        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin removed from agent successfully" });
    }

    [HttpDelete("{pluginId}")]
    public async Task<IActionResult> DeletePlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new DeletePluginCommand(pluginId, userId);
        await mediator.Send(command, cancellationToken);

        return Ok(new { message = "Plugin deleted successfully" });
    }
}