using Application.UseCases.Plugin.PurchasePlugin;
using Application.UseCases.UserPlugin.GetUserPlugin;
using Application.UseCases.UserPlugin.ListUserPlugins;
using Application.UseCases.UserPlugin.UninstallPlugin;
using Application.UseCases.UserPlugin.UpdatePluginSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/user/plugins")]
[Authorize]
public class UserPluginsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// List all plugins installed by the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListPlugins(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var query = new ListUserPluginsQuery(userId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get details of a specific installed plugin
    /// </summary>
    [HttpGet("{pluginId:guid}")]
    public async Task<IActionResult> GetPlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var query = new GetUserPluginQuery(userId, pluginId);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new { error = "Plugin not found or not installed" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Install a plugin for the current user
    /// </summary>
    [HttpPost("{pluginId:guid}/install")]
    public async Task<IActionResult> InstallPlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new PurchasePluginCommand(userId, pluginId);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin installed successfully" });
    }

    /// <summary>
    /// Uninstall a plugin for the current user
    /// </summary>
    [HttpDelete("{pluginId:guid}/uninstall")]
    public async Task<IActionResult> UninstallPlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new UninstallPluginCommand(userId, pluginId);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin uninstalled successfully" });
    }

    /// <summary>
    /// Update plugin-specific settings for the current user
    /// </summary>
    [HttpPut("{pluginId:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(
        Guid pluginId,
        [FromBody] UpdatePluginSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var command = new UpdatePluginSettingsCommand(userId, pluginId, request.Settings);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin settings updated successfully" });
    }
}

public class UpdatePluginSettingsRequest
{
    public Dictionary<string, object>? Settings { get; set; }
}
