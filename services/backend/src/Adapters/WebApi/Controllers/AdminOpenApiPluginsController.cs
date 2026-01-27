using Application.UseCases.OpenApiPlugin.Common;
using Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.DeleteOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.GetOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.ListOpenApiPlugins;
using Application.UseCases.OpenApiPlugin.RefreshOpenApiPluginSpec;
using Application.UseCases.OpenApiPlugin.UpdateOpenApiPlugin;
using Application.UseCases.OpenApiPlugin.ValidateOpenApiSpec;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for admin OpenAPI plugin management operations.
/// Provides CRUD endpoints for managing OpenAPI-type plugins.
/// </summary>
[ApiController]
[Route("api/admin/openapi-plugins")]
[Authorize(Roles = "admin")]
public class AdminOpenApiPluginsController(
    IMediator mediator,
    ILogger<AdminOpenApiPluginsController> logger) : ControllerBase
{
    /// <summary>
    /// Get all OpenAPI plugins.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllPlugins(CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ListOpenApiPluginsQuery(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving OpenAPI plugins");
            return StatusCode(500, new { error = "An error occurred while retrieving OpenAPI plugins" });
        }
    }

    /// <summary>
    /// Get a specific OpenAPI plugin by ID, including its available functions.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetPluginById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new GetOpenApiPluginQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "OpenAPI plugin not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving OpenAPI plugin {PluginId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the OpenAPI plugin" });
        }
    }

    /// <summary>
    /// Create a new OpenAPI plugin.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreatePlugin(
        [FromBody] CreateOpenApiPluginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateOpenApiPluginCommand(
                request.Name,
                request.SystemName,
                request.BaseUrl,
                request.Description,
                request.OpenApiSpecUrl,
                request.TimeoutSeconds,
                request.PriceCredits,
                request.IsSubscriptionBased,
                request.IsActive);

            var result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetPluginById), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, parameter = ex.ParameterName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating OpenAPI plugin {PluginName}", request.Name);
            return StatusCode(500, new { error = "An error occurred while creating the OpenAPI plugin" });
        }
    }

    /// <summary>
    /// Update an existing OpenAPI plugin.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdatePlugin(
        Guid id,
        [FromBody] UpdateOpenApiPluginRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateOpenApiPluginCommand(
                id,
                request.Name,
                request.Description,
                request.BaseUrl,
                request.OpenApiSpecUrl,
                request.TimeoutSeconds,
                request.PriceCredits,
                request.IsSubscriptionBased,
                request.IsActive);

            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "OpenAPI plugin not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, parameter = ex.ParameterName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating OpenAPI plugin {PluginId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the OpenAPI plugin" });
        }
    }

    /// <summary>
    /// Delete an OpenAPI plugin.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePlugin(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteOpenApiPluginCommand(id), cancellationToken);
            return NoContent();
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "OpenAPI plugin not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting OpenAPI plugin {PluginId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the OpenAPI plugin" });
        }
    }

    /// <summary>
    /// Force refresh the OpenAPI spec cache for a plugin.
    /// </summary>
    [HttpPost("{id:guid}/refresh")]
    public async Task<IActionResult> RefreshSpec(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new RefreshOpenApiPluginSpecCommand(id),
                cancellationToken);
            return Ok(result);
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "OpenAPI plugin not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error refreshing OpenAPI spec for plugin {PluginId}", id);
            return StatusCode(500, new { error = "An error occurred while refreshing the OpenAPI spec" });
        }
    }

    /// <summary>
    /// Validate an OpenAPI spec URL without creating a plugin.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateSpec(
        [FromBody] ValidateOpenApiSpecRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new ValidateOpenApiSpecQuery(request.BaseUrl, request.OpenApiSpecUrl),
                cancellationToken);
            return Ok(result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, parameter = ex.ParameterName });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating OpenAPI spec at {BaseUrl}", request.BaseUrl);
            return StatusCode(500, new { error = "An error occurred while validating the OpenAPI spec" });
        }
    }
}

/// <summary>
/// Request body for creating an OpenAPI plugin.
/// </summary>
public record CreateOpenApiPluginRequest(
    string Name,
    string SystemName,
    string BaseUrl,
    string? Description = null,
    string? OpenApiSpecUrl = null,
    int? TimeoutSeconds = null,
    int PriceCredits = 10,
    bool IsSubscriptionBased = false,
    bool IsActive = false);

/// <summary>
/// Request body for updating an OpenAPI plugin.
/// </summary>
public record UpdateOpenApiPluginRequest(
    string? Name = null,
    string? Description = null,
    string? BaseUrl = null,
    string? OpenApiSpecUrl = null,
    int? TimeoutSeconds = null,
    int? PriceCredits = null,
    bool? IsSubscriptionBased = null,
    bool? IsActive = null);

/// <summary>
/// Request body for validating an OpenAPI spec.
/// </summary>
public record ValidateOpenApiSpecRequest(
    string BaseUrl,
    string? OpenApiSpecUrl = null);
