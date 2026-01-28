namespace Application.UseCases.Publisher.Common;

public sealed record PluginStatsDto
{
    public Guid PluginId { get; init; }
    public string PluginName { get; init; } = string.Empty;
    public int TotalInstalls { get; init; }
    public double AverageRating { get; init; }
    public int TotalReviews { get; init; }
    public int ActiveUsers { get; init; }
    public DateTime? LastInstallAt { get; init; }
}
