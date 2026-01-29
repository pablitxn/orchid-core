namespace Application.UseCases.PluginAnalytics.GetPluginUsageStats;

public sealed record PluginUsageStatsDto
{
    public Guid PluginId { get; init; }
    public string Period { get; init; } = string.Empty;
    public int Installs { get; init; }
    public int Uninstalls { get; init; }
    public int ActiveUsers { get; init; }
    public int Invocations { get; init; }
    public double ErrorRate { get; init; }
    public double AvgRating { get; init; }
    public int ReviewCount { get; init; }
    public List<DailyStatsDto> DailyBreakdown { get; init; } = new();
}

public sealed record DailyStatsDto
{
    public DateTime Date { get; init; }
    public int Invocations { get; init; }
    public int Errors { get; init; }
    public int ActiveUsers { get; init; }
}
