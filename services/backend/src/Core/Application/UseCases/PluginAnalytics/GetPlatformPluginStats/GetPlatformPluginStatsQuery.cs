using MediatR;

namespace Application.UseCases.PluginAnalytics.GetPlatformPluginStats;

public record GetPlatformPluginStatsQuery(
    string Period = "last_30_days",
    int TopN = 10
) : IRequest<PlatformPluginStatsDto>;
