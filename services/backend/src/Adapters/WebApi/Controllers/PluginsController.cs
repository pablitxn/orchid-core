using Application.UseCases.Plugin.CreatePlugin;
using Application.UseCases.Plugin.ListPlugins;
using Application.UseCases.Plugin.TogglePlugin;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PluginsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetPlugins()
    {
        var plugins = await _mediator.Send(new ListPluginsQuery());
        return Ok(plugins);
    }

    [HttpPost]
    public async Task<IActionResult> InstallPlugin([FromBody] CreatePluginCommand command)
    {
        var plugin = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetPlugins), new { id = plugin.Id }, plugin);
    }

    [HttpPost("{id}/activate")]
    public async Task<IActionResult> Activate(Guid id)
    {
        await _mediator.Send(new TogglePluginCommand(id, true));
        return Ok();
    }

    [HttpPost("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        await _mediator.Send(new TogglePluginCommand(id, false));
        return Ok();
    }
}