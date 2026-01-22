using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Core.Application.UseCases.Dashboard.Common;
using Core.Domain.Entities;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Core.Application.UseCases.Dashboard.GetDashboardOverview;

public sealed class GetDashboardOverviewHandler(
    ISubscriptionRepository subscriptionRepository,
    ICreditConsumptionRepository creditConsumptionRepository,
    ILogger<GetDashboardOverviewHandler> logger)
    : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewDto>
{
    public async Task<DashboardOverviewDto> Handle(GetDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;

        logger.LogInformation("Starting dashboard overview for user {UserId}", userId);

        try
        {
            logger.LogDebug("Fetching subscription for user {UserId}", userId);
            var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);

            logger.LogDebug("Fetching credit history for user {UserId}", userId);
            var creditHistory = await creditConsumptionRepository.GetRecentByUserIdAsync(userId, 30, cancellationToken);

            logger.LogDebug("Building dashboard metrics for user {UserId}", userId);

            return new DashboardOverviewDto
            {
                Credits = await BuildCreditMetrics(subscription, creditHistory, cancellationToken),
                Subscription = BuildSubscriptionMetrics(subscription),
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while fetching dashboard overview for user {UserId}", userId);
            throw;
        }
    }

    private static Task<CreditMetricsDto> BuildCreditMetrics(
        SubscriptionEntity? subscription,
        List<CreditConsumptionEntity> creditHistory,
        CancellationToken cancellationToken)
    {
        var currentBalance = subscription?.Credits ?? 0;
        var now = DateTime.UtcNow;
        
        var consumedToday = creditHistory
            .Where(c => c.ConsumedAt.Date == now.Date)
            .Sum(c => c.CreditsConsumed);
            
        var consumedThisWeek = creditHistory
            .Where(c => c.ConsumedAt >= now.AddDays(-7))
            .Sum(c => c.CreditsConsumed);
            
        var consumedThisMonth = creditHistory
            .Where(c => c.ConsumedAt.Month == now.Month && c.ConsumedAt.Year == now.Year)
            .Sum(c => c.CreditsConsumed);

        var last30Days = creditHistory
            .Where(c => c.ConsumedAt >= now.AddDays(-30))
            .ToList();

        var averageDailyConsumption = last30Days.Any() 
            ? (decimal)last30Days.Sum(c => c.CreditsConsumed) / 30 
            : 0;

        var daysUntilExhaustion = averageDailyConsumption > 0 
            ? (int)(currentBalance / averageDailyConsumption) 
            : int.MaxValue;

        var consumptionByType = creditHistory
            .GroupBy(c => c.ConsumptionType)
            .Select(g => new CreditConsumptionByTypeDto
            {
                Type = g.Key,
                CreditsConsumed = g.Sum(c => c.CreditsConsumed),
                Percentage = creditHistory.Any() 
                    ? (decimal)g.Sum(c => c.CreditsConsumed) / creditHistory.Sum(c => c.CreditsConsumed) * 100 
                    : 0
            })
            .OrderByDescending(c => c.CreditsConsumed)
            .ToList();

        var topResources = creditHistory
            .GroupBy(c => new { c.ResourceId, c.ResourceName, c.ConsumptionType })
            .Select(g => new TopResourceConsumptionDto
            {
                // ResourceId = g.Key.ResourceId,
                ResourceName = g.Key.ResourceName,
                ResourceType = g.Key.ConsumptionType,
                CreditsConsumed = g.Sum(c => c.CreditsConsumed)
            })
            .OrderByDescending(r => r.CreditsConsumed)
            .Take(5)
            .ToList();

        var alertThreshold = 100; // TODO: Get from user preferences
        var lowCreditAlert = currentBalance < alertThreshold;

        return Task.FromResult(new CreditMetricsDto
        {
            CurrentBalance = currentBalance,
            ConsumedToday = consumedToday,
            ConsumedThisWeek = consumedThisWeek,
            ConsumedThisMonth = consumedThisMonth,
            AverageDailyConsumption = averageDailyConsumption,
            DaysUntilExhaustion = daysUntilExhaustion,
            ConsumptionByType = consumptionByType,
            TopConsumingResources = topResources,
            LowCreditAlert = lowCreditAlert,
            AlertThreshold = alertThreshold
        });
    }

    private SubscriptionMetricsDto BuildSubscriptionMetrics(SubscriptionEntity? subscription)
    {
        if (subscription == null)
        {
            return new SubscriptionMetricsDto
            {
                PlanName = "Free",
                IncludedCredits = 0,
                ExpiresAt = DateTime.UtcNow,
                DaysUntilExpiration = 0,
                AutoRenew = false,
                Price = 0,
                PaymentUnit = "month",
                RecommendedPlan = "Basic"
            };
        }

        // var daysUntilExpiration = (int)(subscription.ExpiresAt - DateTime.UtcNow).TotalDays;
        
        // TODO: Implement plan recommendation logic based on usage patterns
        var recommendedPlan = subscription.Credits < 500 ? "Pro" : "Basic";

        return new SubscriptionMetricsDto
        {
            PlanName = "Basic", // TODO: Get from subscription plan
            IncludedCredits = subscription.Credits,
            // ExpiresAt = subscription.ExpiresAt,
            // DaysUntilExpiration = Math.Max(0, daysUntilExpiration),
            AutoRenew = subscription.AutoRenew,
            Price = 0, // TODO: Get from subscription plan
            PaymentUnit = "month",
            RecommendedPlan = recommendedPlan
        };
    }

}