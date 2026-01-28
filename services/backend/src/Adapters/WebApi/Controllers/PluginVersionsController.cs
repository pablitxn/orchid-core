using Application.UseCases.PluginVersion.ListPluginVersions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/marketplace/plugins/{pluginId:guid}/versions")]
public class PluginVersionsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// List all versions of a plugin.
    /// Returns versions ordered by semantic version descending (newest first).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ListVersions(Guid pluginId, CancellationToken cancellationToken)
    {
        var query = new ListPluginVersionsQuery(pluginId);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
