using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.FeatureFlag.ListFeatureFlags;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeaturesController(IMediator mediator, ISubscriptionRepository subscriptionRepository) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeatures(CancellationToken cancellationToken)
    {
        Guid? userId = null;
        string? userPlan = null;

        // Try to get user context if authenticated
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
        {
            userId = userGuid;

            // Get user's subscription plan
            var subscription = await subscriptionRepository.GetByUserIdAsync(userGuid, cancellationToken);
            if (subscription?.SubscriptionPlan != null)
            {
                userPlan = subscription.SubscriptionPlan.PlanEnum.ToString();
            }
        }

        var query = new ListFeatureFlagsQuery(userId, userPlan);
        var result = await mediator.Send(query, cancellationToken);

        return Ok(new { features = result.Features });
    }

    [HttpGet("{key}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeature(string key, CancellationToken cancellationToken)
    {
        Guid? userId = null;
        string? userPlan = null;

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var userGuid))
        {
            userId = userGuid;

            var subscription = await subscriptionRepository.GetByUserIdAsync(userGuid, cancellationToken);
            if (subscription?.SubscriptionPlan != null)
            {
                userPlan = subscription.SubscriptionPlan.PlanEnum.ToString();
            }
        }

        var query = new ListFeatureFlagsQuery(userId, userPlan);
        var result = await mediator.Send(query, cancellationToken);

        if (result.Features.TryGetValue(key, out var enabled))
        {
            return Ok(new { key, enabled });
        }

        return Ok(new { key, enabled = false });
    }
}
