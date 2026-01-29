using Application.UseCases.PluginAnalytics.GetPluginUsageStats;
using Application.UseCases.Publisher.CreatePluginVersion;
using Application.UseCases.Publisher.GetPluginStats;
using Application.UseCases.Publisher.GetPublisherProfile;
using Application.UseCases.Publisher.ListPublisherPlugins;
using Application.UseCases.Publisher.RegisterPublisher;
using Application.UseCases.Publisher.SubmitPlugin;
using Application.UseCases.Publisher.UpdatePublisherPlugin;
using Application.UseCases.Publisher.UpdatePublisherProfile;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/publishers")]
[Authorize]
public class PublishersController(IMediator mediator, ILogger<PublishersController> logger) : ControllerBase
{
    /// <summary>
    /// Register as a publisher
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPublisherRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new RegisterPublisherCommand(
                userId,
                request.Name,
                request.Bio,
                request.WebsiteUrl,
                request.LogoUrl,
                request.SupportEmail);
            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(GetProfile), result);
        }
        catch (UserNotFoundException)
        {
            return NotFound(new { error = "User not found" });
        }
        catch (PublisherAlreadyExistsException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get current user's publisher profile
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var query = new GetPublisherProfileQuery(userId);
        var result = await mediator.Send(query, ct);

        if (result is null)
        {
            return NotFound(new { error = "Publisher profile not found. Register first." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Update current user's publisher profile
    /// </summary>
    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdatePublisherRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new UpdatePublisherProfileCommand(
                userId,
                request.Name,
                request.Bio,
                request.WebsiteUrl,
                request.LogoUrl,
                request.SupportEmail);
            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
    }

    /// <summary>
    /// Submit a new plugin for review
    /// </summary>
    [HttpPost("plugins")]
    public async Task<IActionResult> SubmitPlugin(
        [FromBody] SubmitPluginRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new SubmitPluginCommand(
                userId,
                request.Name,
                request.Slug,
                request.ShortDescription,
                request.FullDescription,
                request.Category,
                request.Tags,
                request.Version,
                request.ManifestUrl,
                request.RepositoryUrl,
                request.Compatibility);
            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(ListPlugins), result);
        }
        catch (PublisherNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DuplicatePluginSlugException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>
    /// List all plugins for current publisher
    /// </summary>
    [HttpGet("plugins")]
    public async Task<IActionResult> ListPlugins(CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var query = new ListPublisherPluginsQuery(userId);
            var result = await mediator.Send(query, ct);
            return Ok(result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
    }

    /// <summary>
    /// Submit an update for an existing plugin (creates a new submission for review)
    /// </summary>
    [HttpPut("plugins/{pluginId:guid}")]
    public async Task<IActionResult> UpdatePlugin(
        Guid pluginId,
        [FromBody] UpdatePluginRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new UpdatePublisherPluginCommand(
                userId,
                pluginId,
                request.Name,
                request.ShortDescription,
                request.FullDescription,
                request.Category,
                request.Tags,
                request.ManifestUrl,
                request.RepositoryUrl,
                request.Compatibility);
            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Create a new version for an existing plugin (creates a new submission for review)
    /// </summary>
    [HttpPost("plugins/{pluginId:guid}/versions")]
    public async Task<IActionResult> CreateVersion(
        Guid pluginId,
        [FromBody] CreateVersionRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new CreatePluginVersionCommand(
                userId,
                pluginId,
                request.Version,
                request.ReleaseNotes,
                request.ManifestUrl,
                request.Compatibility);
            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(ListPlugins), result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Get stats for a specific plugin
    /// </summary>
    [HttpGet("plugins/{pluginId:guid}/stats")]
    public async Task<IActionResult> GetPluginStats(Guid pluginId, CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var query = new GetPluginStatsQuery(userId, pluginId);
            var result = await mediator.Send(query, ct);
            return Ok(result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Get detailed usage analytics for a specific plugin
    /// </summary>
    [HttpGet("plugins/{pluginId:guid}/analytics")]
    public async Task<IActionResult> GetPluginAnalytics(
        Guid pluginId,
        [FromQuery] string period = "last_30_days",
        CancellationToken ct = default)
    {
        try
        {
            var userId = User.GetUserId();
            var query = new GetPluginUsageStatsQuery(userId, pluginId, period);
            var result = await mediator.Send(query, ct);
            return Ok(result);
        }
        catch (PublisherNotFoundException)
        {
            return NotFound(new { error = "Publisher profile not found" });
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
    }
}

// Request DTOs
public sealed record RegisterPublisherRequest(
    string Name,
    string? Bio,
    string? WebsiteUrl,
    string? LogoUrl,
    string? SupportEmail);

public sealed record UpdatePublisherRequest(
    string? Name,
    string? Bio,
    string? WebsiteUrl,
    string? LogoUrl,
    string? SupportEmail);

public sealed record SubmitPluginRequest(
    string Name,
    string Slug,
    string ShortDescription,
    string? FullDescription,
    string Category,
    string? Tags,
    string Version,
    string? ManifestUrl,
    string? RepositoryUrl,
    string? Compatibility);

public sealed record UpdatePluginRequest(
    string? Name,
    string? ShortDescription,
    string? FullDescription,
    string? Category,
    string? Tags,
    string? ManifestUrl,
    string? RepositoryUrl,
    string? Compatibility);

public sealed record CreateVersionRequest(
    string Version,
    string? ReleaseNotes,
    string? ManifestUrl,
    string? Compatibility);
