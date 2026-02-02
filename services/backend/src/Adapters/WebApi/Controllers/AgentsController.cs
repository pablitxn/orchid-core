using Application.UseCases.Agent.AddPluginToAgent;
using Application.UseCases.Agent.CreateAgent;
using Application.UseCases.Agent.GetAgentPlugins;
using Application.UseCases.Agent.GetAgentRatings;
using Application.UseCases.Agent.ListAgents;
using Application.UseCases.Agent.ListRecycleBinAgents;
using Application.UseCases.Agent.PermanentDeleteAgent;
using Application.UseCases.Agent.RateAgent;
using Application.UseCases.Agent.RemovePluginFromAgent;
using Application.UseCases.Agent.RestoreAgent;
using Application.UseCases.Agent.SoftDeleteAgent;
using Application.UseCases.Agent.UpdateAgent;
using Application.UseCases.Agent.VerifyAgentAccess;
using Application.UseCases.User.GetUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;

namespace WebApi.Controllers;

public class CreateAgentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Personality { get; set; }
    public Guid? PersonalityTemplateId { get; set; }
    public string? Language { get; set; }
    public string? Model { get; set; }
    public Guid[]? PluginIds { get; set; }
    public bool IsPublic { get; set; } = false;
}

public class UpdateAgentRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Personality { get; set; }
    public Guid? PersonalityTemplateId { get; set; }
    public string? Language { get; set; }
    public string? Model { get; set; }
    public Guid[]? PluginIds { get; set; }
    public bool? IsPublic { get; set; }
}

public class RateAgentRequest
{
    /// <summary>
    /// Rating value from 1 to 5
    /// </summary>
    public int Rating { get; set; }

    /// <summary>
    /// Optional comment/review
    /// </summary>
    public string? Comment { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class AgentsController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    private async Task<Guid?> ResolveUserIdAsync()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
        {
            // Dev auth defaults to 00000000-0000-0000-0000-000000000001 which isn't seeded.
            // Prefer resolving by email in that case to avoid FK violations.
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

        var user = await _mediator.Send(new GetUserQuery(email));
        return user?.Id;
    }

    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        // Try to get the current user ID (optional authentication)
        var userId = await ResolveUserIdAsync();

        var agents = await _mediator.Send(new ListAgentsQuery(userId));
        return Ok(agents);
    }

    [HttpGet("my-agents")]
    [Authorize]
    public async Task<IActionResult> GetMyAgents()
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var agents = await _mediator.Send(new ListAgentsQuery(userGuid.Value));
        return Ok(agents);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateAgent([FromBody] CreateAgentRequest request)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new CreateAgentCommand(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.Personality,
            request.PersonalityTemplateId,
            request.Language,
            request.Model,
            request.PluginIds,
            userGuid.Value,
            request.IsPublic
        );

        var agent = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetAgents), new { id = agent.Id }, agent);
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateAgent(Guid id, [FromBody] UpdateAgentRequest request)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new UpdateAgentCommand(
            id,
            userGuid.Value,
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.Personality,
            request.PersonalityTemplateId,
            request.Language,
            request.Model,
            request.PluginIds,
            request.IsPublic
        );

        var agent = await _mediator.Send(command);
        return Ok(agent);
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> SoftDeleteAgent(Guid id)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        await _mediator.Send(new SoftDeleteAgentCommand(id, userGuid.Value));
        return NoContent();
    }

    [HttpGet("recycle-bin")]
    [Authorize]
    public async Task<IActionResult> GetRecycleBinAgents()
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var agents = await _mediator.Send(new ListRecycleBinAgentsQuery(userGuid.Value));
        return Ok(agents);
    }

    [HttpPost("{id}/restore")]
    [Authorize]
    public async Task<IActionResult> RestoreAgent(Guid id)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        await _mediator.Send(new RestoreAgentCommand(id, userGuid.Value));
        return NoContent();
    }

    [HttpDelete("{id}/permanent")]
    [Authorize]
    public async Task<IActionResult> PermanentDeleteAgent(Guid id)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        await _mediator.Send(new PermanentDeleteAgentCommand(id, userGuid.Value));
        return NoContent();
    }

    /// <summary>
    /// Rate an agent. Users can rate an agent once and update their rating.
    /// </summary>
    [HttpPost("{id}/rate")]
    [Authorize]
    public async Task<IActionResult> RateAgent(Guid id, [FromBody] RateAgentRequest request)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new RateAgentCommand(id, userGuid.Value, request.Rating, request.Comment);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Get all ratings for an agent including average rating and total count
    /// </summary>
    [HttpGet("{id}/ratings")]
    public async Task<IActionResult> GetAgentRatings(Guid id)
    {
        var query = new GetAgentRatingsQuery(id);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Verify if the current user has access to use an agent
    /// </summary>
    [HttpGet("{id}/verify-access")]
    [Authorize]
    public async Task<IActionResult> VerifyAgentAccess(Guid id)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var isAdmin = User.IsInRole("admin") || string.Equals(email, "admin@example.com", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            return Ok(new VerifyAgentAccessDto(true));
        }

        var query = new VerifyAgentAccessQuery(userGuid.Value, id);
        var result = await _mediator.Send(query);

        if (!result.HasAccess)
        {
            return StatusCode(403, result);
        }

        return Ok(result);
    }

    /// <summary>
    /// Get all plugins enabled for an agent
    /// </summary>
    [HttpGet("{id:guid}/plugins")]
    [Authorize]
    public async Task<IActionResult> GetAgentPlugins(Guid id)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var query = new GetAgentPluginsQuery(id, userGuid.Value);
        var result = await _mediator.Send(query);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(result.Plugins);
    }

    /// <summary>
    /// Enable a plugin for an agent
    /// </summary>
    [HttpPost("{agentId:guid}/plugins/{pluginId:guid}/enable")]
    [Authorize]
    public async Task<IActionResult> EnablePluginForAgent(Guid agentId, Guid pluginId)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new AddPluginToAgentCommand(userGuid.Value, agentId, pluginId);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin enabled for agent successfully" });
    }

    /// <summary>
    /// Disable a plugin for an agent
    /// </summary>
    [HttpDelete("{agentId:guid}/plugins/{pluginId:guid}/disable")]
    [Authorize]
    public async Task<IActionResult> DisablePluginForAgent(Guid agentId, Guid pluginId)
    {
        var userGuid = await ResolveUserIdAsync();
        if (userGuid is null)
        {
            return Unauthorized("Invalid user identity");
        }

        var command = new RemovePluginFromAgentCommand(userGuid.Value, agentId, pluginId);
        var result = await _mediator.Send(command);

        if (!result.Success)
        {
            return BadRequest(new { error = result.ErrorMessage });
        }

        return Ok(new { message = "Plugin disabled for agent successfully" });
    }
}
