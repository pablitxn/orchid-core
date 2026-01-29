using MediatR;

namespace Application.UseCases.PluginAnalytics.GetPluginUsageStats;

public record GetPluginUsageStatsQuery(
    Guid UserId,
    Guid PluginId,
    string Period = "last_30_days"
) : IRequest<PluginUsageStatsDto>;
