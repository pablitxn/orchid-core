using Application.Interfaces;
using MediatR;

namespace Application.UseCases.PluginAnalytics.GetPlatformPluginStats;

public sealed class GetPlatformPluginStatsHandler(
    IPluginUsageEventRepository usageRepository,
    IPluginRepository pluginRepository
) : IRequestHandler<GetPlatformPluginStatsQuery, PlatformPluginStatsDto>
{
    private readonly IPluginUsageEventRepository _usageRepository = usageRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<PlatformPluginStatsDto> Handle(GetPlatformPluginStatsQuery request, CancellationToken cancellationToken)
    {
        var (startDate, endDate) = GetDateRange(request.Period);

        // Get platform-wide stats
        var platformStats = await _usageRepository.GetPlatformStatsAsync(startDate, endDate, cancellationToken);

        // Calculate error rate
        var errorRate = platformStats.TotalInvocations > 0
            ? (double)platformStats.TotalErrors / platformStats.TotalInvocations
            : 0.0;

        // Get top plugins
        var topByInstalls = await _usageRepository.GetTopPluginsByInstallsAsync(
            startDate, endDate, request.TopN, cancellationToken);

        var topByInvocations = await _usageRepository.GetTopPluginsByInvocationsAsync(
            startDate, endDate, request.TopN, cancellationToken);

        // Get top by rating from plugin repository (pre-computed)
        var allPlugins = await _pluginRepository.ListActiveAsync(cancellationToken);
        var topByRating = allPlugins
            .OrderByDescending(p => p.Rating)
            .ThenByDescending(p => p.ReviewCount)
            .Take(request.TopN)
            .Select(p => new TopPluginDto
            {
                PluginId = p.Id,
                Name = p.Name,
                Value = (int)(p.Rating * 100) // Store as int for consistency
            })
            .ToList();

        return new PlatformPluginStatsDto
        {
            Period = request.Period,
            TotalPlugins = platformStats.TotalPlugins,
            TotalInstalls = platformStats.TotalInstalls,
            TotalUninstalls = platformStats.TotalUninstalls,
            TotalInvocations = platformStats.TotalInvocations,
            PlatformActiveUsers = platformStats.PlatformActiveUsers,
            AverageErrorRate = Math.Round(errorRate, 4),
            TopByInstalls = topByInstalls.Select(t => new TopPluginDto
            {
                PluginId = t.PluginId,
                Name = t.PluginName,
                Value = t.Value
            }).ToList(),
            TopByInvocations = topByInvocations.Select(t => new TopPluginDto
            {
                PluginId = t.PluginId,
                Name = t.PluginName,
                Value = t.Value
            }).ToList(),
            TopByRating = topByRating
        };
    }

    private static (DateTime startDate, DateTime endDate) GetDateRange(string period)
    {
        var endDate = DateTime.UtcNow;
        var startDate = period switch
        {
            "last_7_days" => endDate.AddDays(-7),
            "last_30_days" => endDate.AddDays(-30),
            "last_90_days" => endDate.AddDays(-90),
            "all_time" => DateTime.MinValue,
            _ => endDate.AddDays(-30)
        };

        return (startDate, endDate);
    }
}
