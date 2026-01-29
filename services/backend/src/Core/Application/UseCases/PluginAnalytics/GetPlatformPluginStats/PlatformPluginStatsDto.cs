namespace Application.UseCases.PluginAnalytics.GetPlatformPluginStats;

public sealed record PlatformPluginStatsDto
{
    public string Period { get; init; } = string.Empty;
    public int TotalPlugins { get; init; }
    public int TotalInstalls { get; init; }
    public int TotalUninstalls { get; init; }
    public int TotalInvocations { get; init; }
    public int PlatformActiveUsers { get; init; }
    public double AverageErrorRate { get; init; }
    public List<TopPluginDto> TopByInstalls { get; init; } = new();
    public List<TopPluginDto> TopByInvocations { get; init; } = new();
    public List<TopPluginDto> TopByRating { get; init; } = new();
}

public sealed record TopPluginDto
{
    public Guid PluginId { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Value { get; init; }
}
