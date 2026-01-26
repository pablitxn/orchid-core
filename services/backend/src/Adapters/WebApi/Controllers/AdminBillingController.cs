using System.Security.Claims;
using Application.UseCases.AdminBilling.AdjustCredits;
using Application.UseCases.AdminBilling.GetAllSubscriptions;
using Application.UseCases.AdminBilling.GetSubscriptionByUserId;
using Application.UseCases.AdminBilling.GetWebhookLogs;
using Application.UseCases.AdminBilling.RetryWebhook;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for admin billing management operations.
/// Provides endpoints for viewing subscriptions, adjusting credits, and managing webhooks.
/// </summary>
[ApiController]
[Route("api/admin/billing")]
[Authorize(Roles = "admin")]
public class AdminBillingController(IMediator mediator, ILogger<AdminBillingController> logger) : ControllerBase
{
    /// <summary>
    /// Get all subscriptions with pagination.
    /// </summary>
    [HttpGet("subscriptions")]
    public async Task<IActionResult> GetAllSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await mediator.Send(new GetAllSubscriptionsQuery(page, pageSize));
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving subscriptions");
            return StatusCode(500, new { error = "An error occurred while retrieving subscriptions" });
        }
    }

    /// <summary>
    /// Get a specific user's subscription details.
    /// </summary>
    [HttpGet("subscriptions/{userId:guid}")]
    public async Task<IActionResult> GetSubscriptionByUserId(Guid userId)
    {
        try
        {
            var result = await mediator.Send(new GetSubscriptionByUserIdQuery(userId));
            return Ok(result);
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found for the specified user" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving subscription for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving the subscription" });
        }
    }

    /// <summary>
    /// Manually adjust a user's credits.
    /// </summary>
    [HttpPost("credits/adjust")]
    public async Task<IActionResult> AdjustCredits([FromBody] AdjustCreditsRequest request)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new AdjustCreditsCommand(
                request.UserId,
                request.Amount,
                request.Reason,
                adminUserId.Value));

            return Ok(result);
        }
        catch (SubscriptionNotFoundException)
        {
            return NotFound(new { error = "Subscription not found for the specified user" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InsufficientCreditsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adjusting credits for user {UserId}", request.UserId);
            return StatusCode(500, new { error = "An error occurred while adjusting credits" });
        }
    }

    /// <summary>
    /// Get webhook processing logs with optional filters.
    /// </summary>
    [HttpGet("webhooks/logs")]
    public async Task<IActionResult> GetWebhookLogs(
        [FromQuery] WebhookEventStatus? status = null,
        [FromQuery] string? eventType = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            var result = await mediator.Send(new GetWebhookLogsQuery(
                status,
                eventType,
                startDate,
                endDate,
                page,
                pageSize));

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving webhook logs");
            return StatusCode(500, new { error = "An error occurred while retrieving webhook logs" });
        }
    }

    /// <summary>
    /// Retry a failed webhook event.
    /// </summary>
    [HttpPost("webhooks/{id:guid}/retry")]
    public async Task<IActionResult> RetryWebhook(Guid id)
    {
        var adminUserId = GetUserId();
        if (adminUserId is null)
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new RetryWebhookCommand(id, adminUserId.Value));
            return Ok(result);
        }
        catch (EntityNotFoundException)
        {
            return NotFound(new { error = "Webhook event not found" });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (OperationFailedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrying webhook {WebhookId}", id);
            return StatusCode(500, new { error = "An error occurred while retrying the webhook" });
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }
}

/// <summary>
/// Request body for credit adjustment.
/// </summary>
public record AdjustCreditsRequest(
    Guid UserId,
    int Amount,
    string Reason);
