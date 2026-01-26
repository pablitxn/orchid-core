using Application.Interfaces;
using Domain.Enums;
using Domain.Events;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Service for managing free tier daily credit allocations.
/// Free tier users get 5 credits per day that reset daily.
/// </summary>
public class FreeTierCreditService(
    ISubscriptionRepository subscriptionRepository,
    ICacheStore cacheStore,
    IEventPublisher eventPublisher,
    ILogger<FreeTierCreditService> logger) : IFreeTierCreditService
{
    private const int DailyFreeCredits = 5;
    private const string CacheKeyPrefix = "free_tier:";
    private const string UsedCreditsKeySuffix = ":used";
    private const string LastResetKeySuffix = ":last_reset";

    public async Task<bool> IsEligibleForFreeTierAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

        // User is eligible if no subscription exists
        if (subscription is null)
            return true;

        // User is eligible if subscription is not active
        return subscription.Status != SubscriptionStatus.Active
            && subscription.Status != SubscriptionStatus.Trialing;
    }

    public async Task<DateTime?> GetLastResetDateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{userId}{LastResetKeySuffix}";
        var cached = await cacheStore.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cached))
            return null;

        return DateTime.TryParse(cached, out var date) ? date : null;
    }

    public async Task GrantDailyCreditsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await IsEligibleForFreeTierAsync(userId, cancellationToken))
        {
            logger.LogDebug("User {UserId} is not eligible for free tier credits", userId);
            return;
        }

        var lastReset = await GetLastResetDateAsync(userId, cancellationToken);
        var today = DateTime.UtcNow.Date;

        // Check if already reset today
        if (lastReset.HasValue && lastReset.Value.Date == today)
        {
            logger.LogDebug("User {UserId} already received free tier credits today", userId);
            return;
        }

        // Reset usage counter and set last reset date
        var usedKey = $"{CacheKeyPrefix}{userId}{UsedCreditsKeySuffix}";
        var resetKey = $"{CacheKeyPrefix}{userId}{LastResetKeySuffix}";

        // Calculate expiry until end of day (UTC)
        var expiryTime = today.AddDays(1) - DateTime.UtcNow;
        if (expiryTime < TimeSpan.Zero)
            expiryTime = TimeSpan.FromDays(1);

        await cacheStore.SetStringAsync(usedKey, "0", expiryTime, cancellationToken);
        await cacheStore.SetStringAsync(resetKey, DateTime.UtcNow.ToString("O"), expiryTime, cancellationToken);

        // Publish event
        await eventPublisher.PublishAsync(new FreeTierCreditsResetEvent(userId, DailyFreeCredits));

        logger.LogInformation("Granted {Credits} free tier credits to user {UserId}", DailyFreeCredits, userId);
    }

    public async Task<int> GetRemainingDailyCreditsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await IsEligibleForFreeTierAsync(userId, cancellationToken))
            return 0;

        // Check if we need to reset for today
        var lastReset = await GetLastResetDateAsync(userId, cancellationToken);
        var today = DateTime.UtcNow.Date;

        if (!lastReset.HasValue || lastReset.Value.Date != today)
        {
            // Grant credits for today
            await GrantDailyCreditsAsync(userId, cancellationToken);
            return DailyFreeCredits;
        }

        // Get used credits
        var usedKey = $"{CacheKeyPrefix}{userId}{UsedCreditsKeySuffix}";
        var usedStr = await cacheStore.GetStringAsync(usedKey, cancellationToken);

        var used = 0;
        if (!string.IsNullOrEmpty(usedStr))
        {
            int.TryParse(usedStr, out used);
        }

        return Math.Max(0, DailyFreeCredits - used);
    }
}

