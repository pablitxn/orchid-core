using MediatR;

namespace Application.UseCases.PluginAnalytics.GetPluginUsageStats;

/// <summary>
/// Admin variant of GetPluginUsageStatsQuery that bypasses ownership check.
/// </summary>
public record GetPluginUsageStatsAdminQuery(
    Guid PluginId,
    string Period = "last_30_days"
) : IRequest<PluginUsageStatsDto>;
