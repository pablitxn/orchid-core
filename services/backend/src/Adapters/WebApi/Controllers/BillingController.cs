using System.Security.Claims;
using Application.UseCases.Billing.GetBillingInvoices;
using Application.UseCases.Billing.GetBillingPaymentMethods;
using Application.UseCases.Billing.GetBillingSubscription;
using Application.UseCases.Billing.GetBillingSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for billing management operations.
/// Provides endpoints for viewing subscription, invoices, payment methods, and billing summary.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize]
public class BillingController(IMediator mediator, ILogger<BillingController> logger) : ControllerBase
{
    /// <summary>
    /// Get current subscription details.
    /// </summary>
    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await mediator.Send(new GetBillingSubscriptionQuery(userId.Value));
        if (result is null)
            return NotFound(new { error = "No subscription found" });

        return Ok(result);
    }

    /// <summary>
    /// Get billing history (invoices).
    /// </summary>
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices([FromQuery] int limit = 10)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new GetBillingInvoicesQuery(userId.Value, limit));
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving invoices for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving invoices" });
        }
    }

    /// <summary>
    /// Get saved payment methods.
    /// </summary>
    [HttpGet("payment-methods")]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new GetBillingPaymentMethodsQuery(userId.Value));
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving payment methods for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving payment methods" });
        }
    }

    /// <summary>
    /// Get comprehensive billing summary.
    /// Includes subscription, credit balance, payment methods count, and latest invoice.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] string? returnUrl = null)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var result = await mediator.Send(new GetBillingSummaryQuery(userId.Value, returnUrl));
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving billing summary for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving billing summary" });
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
