using Application.UseCases.Plugin.ListAvailablePlugins;
using Application.UseCases.Plugin.ListPlugins;
using Application.UseCases.Plugin.PurchasePlugin;
using Application.UseCases.Plugin.DeletePlugin;
using Application.UseCases.Agent.AddPluginToAgent;
using Application.UseCases.Agent.RemovePluginFromAgent;
using Application.UseCases.User.GetUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PluginController(IMediator mediator) : ControllerBase
{
    private async Task<Guid?> ResolveUserIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
        {
            if (userGuid != Guid.Parse("00000000-0000-0000-0000-000000000001"))
            {
                return userGuid;
            }
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var user = await mediator.Send(new GetUserQuery(email));
        return user?.Id;
    }

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
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var query = new ListAvailablePluginsQuery(userGuid.Value);
        var result = await mediator.Send(query, cancellationToken);
        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.IsInRole("admin") || string.Equals(email, "admin@example.com", StringComparison.OrdinalIgnoreCase);
        var plugins = isAdmin
            ? result.Plugins.Select(p => p with { UserOwns = true })
            : result.Plugins;

        // Return plugins in a property matching the front-end expectation
        return Ok(new { availablePlugins = plugins });
    }

    [HttpPost("{pluginId}/purchase")]
    public async Task<IActionResult> PurchasePlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new PurchasePluginCommand(userGuid.Value, pluginId);
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
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new AddPluginToAgentCommand(userGuid.Value, agentId, pluginId);
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
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new RemovePluginFromAgentCommand(userGuid.Value, agentId, pluginId);
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
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new DeletePluginCommand(pluginId, userGuid.Value);
        await mediator.Send(command, cancellationToken);

        return Ok(new { message = "Plugin deleted successfully" });
    }
}
