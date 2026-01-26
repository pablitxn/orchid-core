using Application.UseCases.FeatureFlag;
using Application.UseCases.FeatureFlag.CreateFeatureFlag;
using Application.UseCases.FeatureFlag.DeleteFeatureFlag;
using Application.UseCases.FeatureFlag.GetFeatureFlagById;
using Application.UseCases.FeatureFlag.ListAllFeatureFlags;
using Application.UseCases.FeatureFlag.UpdateFeatureFlag;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for admin feature flag management operations.
/// Provides CRUD endpoints for managing feature flags.
/// </summary>
[ApiController]
[Route("api/admin/features")]
[Authorize(Roles = "admin")]
public class AdminFeaturesController(IMediator mediator, ILogger<AdminFeaturesController> logger) : ControllerBase
{
    /// <summary>
    /// Get all feature flags with full details.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAllFeatureFlags(CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new ListAllFeatureFlagsQuery(), cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving feature flags");
            return StatusCode(500, new { error = "An error occurred while retrieving feature flags" });
        }
    }

    /// <summary>
    /// Get a specific feature flag by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetFeatureFlagById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(new GetFeatureFlagByIdQuery(id), cancellationToken);
            return Ok(result);
        }
        catch (FeatureFlagNotFoundException)
        {
            return NotFound(new { error = "Feature flag not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving feature flag {FeatureFlagId}", id);
            return StatusCode(500, new { error = "An error occurred while retrieving the feature flag" });
        }
    }

    /// <summary>
    /// Create a new feature flag.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateFeatureFlag(
        [FromBody] CreateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new CreateFeatureFlagCommand(
                request.Key,
                request.Description,
                request.IsEnabled,
                request.AllowedPlans,
                request.RolloutPercentage);

            var result = await mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetFeatureFlagById), new { id = result.Id }, result);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating feature flag {FeatureFlagKey}", request.Key);
            return StatusCode(500, new { error = "An error occurred while creating the feature flag" });
        }
    }

    /// <summary>
    /// Update an existing feature flag.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateFeatureFlag(
        Guid id,
        [FromBody] UpdateFeatureFlagRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var command = new UpdateFeatureFlagCommand(
                id,
                request.Description,
                request.IsEnabled,
                request.AllowedPlans,
                request.RolloutPercentage);

            var result = await mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (FeatureFlagNotFoundException)
        {
            return NotFound(new { error = "Feature flag not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating feature flag {FeatureFlagId}", id);
            return StatusCode(500, new { error = "An error occurred while updating the feature flag" });
        }
    }

    /// <summary>
    /// Delete a feature flag.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteFeatureFlag(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await mediator.Send(new DeleteFeatureFlagCommand(id), cancellationToken);
            return NoContent();
        }
        catch (FeatureFlagNotFoundException)
        {
            return NotFound(new { error = "Feature flag not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting feature flag {FeatureFlagId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the feature flag" });
        }
    }
}

/// <summary>
/// Request body for creating a feature flag.
/// </summary>
public record CreateFeatureFlagRequest(
    string Key,
    string? Description,
    bool IsEnabled,
    string? AllowedPlans,
    int RolloutPercentage = 100);

/// <summary>
/// Request body for updating a feature flag.
/// </summary>
public record UpdateFeatureFlagRequest(
    string? Description,
    bool IsEnabled,
    string? AllowedPlans,
    int RolloutPercentage);
