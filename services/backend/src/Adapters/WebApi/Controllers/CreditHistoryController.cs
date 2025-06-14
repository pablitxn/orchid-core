using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CreditHistoryController : ControllerBase
{
    private readonly ICreditConsumptionRepository _creditConsumptionRepository;
    private readonly IMessageCostRepository _messageCostRepository;
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<CreditHistoryController> _logger;

    public CreditHistoryController(
        ICreditConsumptionRepository creditConsumptionRepository,
        IMessageCostRepository messageCostRepository,
        ISubscriptionRepository subscriptionRepository,
        ILogger<CreditHistoryController> logger)
    {
        _creditConsumptionRepository = creditConsumptionRepository;
        _messageCostRepository = messageCostRepository;
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get credit consumption history for the authenticated user
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 50)</param>
    /// <param name="startDate">Filter by start date (optional)</param>
    /// <param name="endDate">Filter by end date (optional)</param>
    [HttpGet("consumption")]
    public async Task<IActionResult> GetConsumptionHistory(
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            IEnumerable<Domain.Entities.CreditConsumptionEntity> consumptions;

            if (startDate.HasValue && endDate.HasValue)
            {
                consumptions = await _creditConsumptionRepository.GetByUserIdAsync(
                    userId.Value, CancellationToken.None);
            }
            else
            {
                consumptions = await _creditConsumptionRepository.GetByUserIdAsync(userId.Value);
            }

            var result = consumptions.Select(c => new
            {
                c.Id,
                c.ConsumptionType,
                c.ResourceId,
                c.ResourceName,
                c.CreditsConsumed,
                c.ConsumedAt,
                c.BalanceAfter,
                c.Metadata
            });

            return Ok(new { consumptions = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consumption history for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving consumption history" });
        }
    }

    /// <summary>
    /// Get message cost history for the authenticated user
    /// </summary>
    /// <param name="limit">Maximum number of records to return (default: 50)</param>
    [HttpGet("messages")]
    public async Task<IActionResult> GetMessageCostHistory([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            var messageCosts = await _messageCostRepository.GetByUserIdAsync(userId.Value, limit);

            var result = messageCosts.Select(m => new
            {
                m.Id,
                m.MessageId,
                m.BillingMethod,
                m.TokensConsumed,
                m.CostPerToken,
                m.FixedRate,
                m.TotalCredits,
                m.HasPluginUsage,
                m.HasWorkflowUsage,
                m.AdditionalCredits,
                m.CreatedAt
            });

            return Ok(new { messageCosts = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving message cost history for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving message cost history" });
        }
    }

    /// <summary>
    /// Get credit consumption summary for the authenticated user
    /// </summary>
    /// <param name="period">Time period: "day", "week", "month", "year" (default: "month")</param>
    [HttpGet("summary")]
    public async Task<IActionResult> GetConsumptionSummary([FromQuery] string period = "month")
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            var since = period.ToLower() switch
            {
                "day" => DateTime.UtcNow.AddDays(-1),
                "week" => DateTime.UtcNow.AddDays(-7),
                "month" => DateTime.UtcNow.AddMonths(-1),
                "year" => DateTime.UtcNow.AddYears(-1),
                _ => DateTime.UtcNow.AddMonths(-1)
            };

            // var totalConsumed = await _creditConsumptionRepository.GetTotalConsumedByUserIdAsync(userId.Value, since);
            var messageCosts = await _messageCostRepository.GetTotalCostByUserIdAsync(userId.Value, since);

            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId.Value);
            var currentBalance = subscription?.Credits ?? 0;

            // Group consumption by type
            // var consumptions = await _creditConsumptionRepository.GetByUserIdAndDateRangeAsync(
            //     userId.Value, since, DateTime.UtcNow);
            //
            // var byType = consumptions
            //     .GroupBy(c => c.ConsumptionType)
            //     .Select(g => new
            //     {
            //         Type = g.Key,
            //         TotalCredits = g.Sum(c => c.CreditsConsumed),
            //         Count = g.Count()
            //     })
            //     .ToList();

            return Ok(new
            {
                period,
                since,
                currentBalance,
                // totalConsumed,
                messageCosts,
                // consumptionByType = byType,
                subscriptionExpiry = subscription?.ExpiresAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving consumption summary for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving consumption summary" });
        }
    }

    /// <summary>
    /// Get current credit balance for the authenticated user
    /// </summary>
    [HttpGet("balance")]
    public async Task<IActionResult> GetCreditBalance()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId.Value);
            
            // Return a default response if no subscription exists yet
            if (subscription == null)
            {
                return Ok(new
                {
                    credits = 0,
                    expiresAt = (DateTime?)null,
                    autoRenew = false,
                    updatedAt = DateTime.UtcNow,
                    hasSubscription = false
                });
            }

            return Ok(new
            {
                credits = subscription.Credits,
                expiresAt = subscription.ExpiresAt,
                autoRenew = subscription.AutoRenew,
                updatedAt = subscription.UpdatedAt,
                hasSubscription = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving credit balance for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving credit balance" });
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}