using Application.Interfaces;
using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditUsage;

public sealed class GetCreditUsageHandler(
    ISubscriptionRepository subscriptionRepository,
    ICreditConsumptionRepository creditConsumptionRepository)
    : IRequestHandler<GetCreditUsageQuery, CreditUsageDto>
{
    public async Task<CreditUsageDto> Handle(
        GetCreditUsageQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var currentBalance = subscription?.Credits ?? 0;

        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Get consumption for different periods
        var todayConsumptions = await creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId, todayStart, now, cancellationToken);
        var weekConsumptions = await creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId, weekStart, now, cancellationToken);
        var monthConsumptions = await creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId, monthStart, now, cancellationToken);

        var consumedToday = todayConsumptions.Sum(c => c.CreditsConsumed);
        var consumedThisWeek = weekConsumptions.Sum(c => c.CreditsConsumed);
        var consumedThisMonth = monthConsumptions.Sum(c => c.CreditsConsumed);

        // Calculate average daily consumption based on period
        var daysInPeriod = request.Period.ToLower() switch
        {
            "day" => 1,
            "week" => 7,
            "month" => DateTime.DaysInMonth(now.Year, now.Month),
            "year" => 365,
            _ => DateTime.DaysInMonth(now.Year, now.Month)
        };

        var averageDaily = daysInPeriod > 0
            ? (double)consumedThisMonth / Math.Min((now - monthStart).Days + 1, daysInPeriod)
            : 0;

        // Calculate days until exhaustion
        int? daysUntilExhaustion = null;
        if (averageDaily > 0 && currentBalance > 0)
        {
            daysUntilExhaustion = (int)Math.Ceiling(currentBalance / averageDaily);
        }

        // Group by consumption type
        var consumptionByType = monthConsumptions
            .GroupBy(c => c.ConsumptionType)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.CreditsConsumed));

        return new CreditUsageDto
        {
            CurrentBalance = currentBalance,
            ConsumedToday = consumedToday,
            ConsumedThisWeek = consumedThisWeek,
            ConsumedThisMonth = consumedThisMonth,
            AverageDailyConsumption = Math.Round(averageDaily, 2),
            DaysUntilExhaustion = daysUntilExhaustion,
            ConsumptionByType = consumptionByType,
            SubscriptionExpiresAt = subscription?.ExpiresAt,
            Period = request.Period
        };
    }
}
