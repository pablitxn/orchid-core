using Application.Interfaces;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BillingPreferenceController : ControllerBase
{
    private readonly IUserBillingPreferenceRepository _billingPreferenceRepository;
    private readonly ILogger<BillingPreferenceController> _logger;

    public BillingPreferenceController(
        IUserBillingPreferenceRepository billingPreferenceRepository,
        ILogger<BillingPreferenceController> logger)
    {
        _billingPreferenceRepository = billingPreferenceRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get billing preferences for the authenticated user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBillingPreferences()
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            var preferences = await _billingPreferenceRepository.GetByUserIdAsync(userId.Value);
            
            if (preferences == null)
            {
                // Return default preferences if none exist
                return Ok(new
                {
                    messageBillingMethod = "tokens",
                    tokenRate = 0.001,
                    fixedMessageRate = 5,
                    lowCreditThreshold = 100,
                    enableLowCreditAlerts = true
                });
            }

            return Ok(new
            {
                preferences.MessageBillingMethod,
                preferences.TokenRate,
                preferences.FixedMessageRate,
                preferences.LowCreditThreshold,
                preferences.EnableLowCreditAlerts,
                preferences.CreatedAt,
                preferences.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving billing preferences for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while retrieving billing preferences" });
        }
    }

    /// <summary>
    /// Update billing preferences for the authenticated user
    /// </summary>
    [HttpPut]
    public async Task<IActionResult> UpdateBillingPreferences([FromBody] UpdateBillingPreferencesRequest request)
    {
        var userId = GetUserId();
        if (!userId.HasValue)
            return Unauthorized();

        try
        {
            // Validate request
            if (request.MessageBillingMethod != "tokens" && request.MessageBillingMethod != "fixed")
            {
                return BadRequest(new { error = "Invalid billing method. Must be 'tokens' or 'fixed'" });
            }

            if (request.TokenRate <= 0 || request.FixedMessageRate <= 0)
            {
                return BadRequest(new { error = "Rates must be positive" });
            }

            var preferences = await _billingPreferenceRepository.GetByUserIdAsync(userId.Value);
            
            if (preferences == null)
            {
                // Create new preferences
                preferences = new UserBillingPreferenceEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId.Value,
                    MessageBillingMethod = request.MessageBillingMethod,
                    TokenRate = request.TokenRate,
                    FixedMessageRate = request.FixedMessageRate,
                    LowCreditThreshold = request.LowCreditThreshold,
                    EnableLowCreditAlerts = request.EnableLowCreditAlerts,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                
                await _billingPreferenceRepository.CreateAsync(preferences);
            }
            else
            {
                // Update existing preferences
                preferences.MessageBillingMethod = request.MessageBillingMethod;
                preferences.TokenRate = request.TokenRate;
                preferences.FixedMessageRate = request.FixedMessageRate;
                preferences.LowCreditThreshold = request.LowCreditThreshold;
                preferences.EnableLowCreditAlerts = request.EnableLowCreditAlerts;
                
                await _billingPreferenceRepository.UpdateAsync(preferences);
            }

            _logger.LogInformation("Updated billing preferences for user {UserId}", userId);

            return Ok(new
            {
                message = "Billing preferences updated successfully",
                preferences = new
                {
                    preferences.MessageBillingMethod,
                    preferences.TokenRate,
                    preferences.FixedMessageRate,
                    preferences.LowCreditThreshold,
                    preferences.EnableLowCreditAlerts,
                    preferences.UpdatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating billing preferences for user {UserId}", userId);
            return StatusCode(500, new { error = "An error occurred while updating billing preferences" });
        }
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}

public class UpdateBillingPreferencesRequest
{
    public string MessageBillingMethod { get; set; } = "tokens";
    public decimal TokenRate { get; set; } = 0.001m;
    public int FixedMessageRate { get; set; } = 5;
    public int? LowCreditThreshold { get; set; } = 100;
    public bool EnableLowCreditAlerts { get; set; } = true;
}