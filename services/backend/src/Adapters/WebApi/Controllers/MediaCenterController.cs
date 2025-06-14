using Application.UseCases.MediaCenter.ListAssets;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/media-center")]
public class MediaCenterController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet("assets")]
    public async Task<IActionResult> GetAssets([FromQuery] string? mimeType)
    {
        var assets = await _mediator.Send(new ListMediaCenterAssetsQuery(mimeType));
        return Ok(assets);
    }
}