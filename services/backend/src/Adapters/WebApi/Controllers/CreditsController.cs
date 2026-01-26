using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Application.UseCases.Credits.GetCreditBalance;
using Application.UseCases.Credits.GetCreditPackages;
using Application.UseCases.Credits.GetCreditTransactions;
using Application.UseCases.Credits.GetCreditUsage;
using Application.UseCases.Credits.PurchaseCredits;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for credit system operations including balance, packages, and purchases.
/// </summary>
[ApiController]
[Route("api/credits")]
[Authorize]
public class CreditsController(IMediator mediator, ILogger<CreditsController> logger) : ControllerBase
{
    /// <summary>
    /// Get current credit balance for the authenticated user.
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        var result = await mediator.Send(new GetCreditBalanceQuery(userId.Value));
        return Ok(result);
    }

    /// <summary>
    /// Get available credit packages for purchase.
    /// </summary>
    [HttpGet("packages")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages()
    {
        var result = await mediator.Send(new GetCreditPackagesQuery());
        return Ok(result);
    }

    /// <summary>
    /// Create a Stripe checkout session to purchase a credit package.
    /// </summary>
    [HttpPost("purchase")]
    public async Task<IActionResult> PurchaseCredits([FromBody] PurchaseCreditsRequest request)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var cmd = new PurchaseCreditsCommand(
                userId.Value,
                request.PackageId,
                request.SuccessUrl,
                request.CancelUrl);

            var result = await mediator.Send(cmd);
            return Ok(new { result.SessionId, result.Url });
        }
        catch (CreditPackageNotFoundException)
        {
            return NotFound("Credit package not found.");
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get credit transaction history for the authenticated user.
    /// </summary>
    [HttpGet("transactions")]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new GetCreditTransactionsQuery(userId.Value, page, pageSize, startDate, endDate);
            var result = await mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving credit transactions for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving transactions" });
        }
    }

    /// <summary>
    /// Get credit usage summary for the authenticated user.
    /// </summary>
    [HttpGet("usage")]
    public async Task<IActionResult> GetUsage([FromQuery] string period = "month")
    {
        var userId = GetUserId();
        if (userId is null)
            return Unauthorized();

        try
        {
            var query = new GetCreditUsageQuery(userId.Value, period);
            var result = await mediator.Send(query);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving credit usage for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving usage" });
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

public record PurchaseCreditsRequest(
    [Required] Guid PackageId,
    [Required] string SuccessUrl,
    [Required] string CancelUrl);
