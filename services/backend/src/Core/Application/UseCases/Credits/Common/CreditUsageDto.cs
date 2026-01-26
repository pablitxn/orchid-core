namespace Application.UseCases.Credits.Common;

public sealed record CreditUsageDto
{
    public int CurrentBalance { get; init; }
    public int ConsumedToday { get; init; }
    public int ConsumedThisWeek { get; init; }
    public int ConsumedThisMonth { get; init; }
    public double AverageDailyConsumption { get; init; }
    public int? DaysUntilExhaustion { get; init; }
    public Dictionary<string, int> ConsumptionByType { get; init; } = new();
    public DateTime? SubscriptionExpiresAt { get; init; }
    public string Period { get; init; } = "month";
}
