using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginUsageEventRepository
{
    Task<PluginUsageEventEntity> CreateAsync(PluginUsageEventEntity entity, CancellationToken cancellationToken = default);

    Task<PluginUsageEventEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<List<PluginUsageEventEntity>> GetByPluginIdAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<int> CountByTypeAsync(
        Guid pluginId,
        string eventType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<int> CountDistinctUsersAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<double> GetErrorRateAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, int>> GetEventCountsByTypeAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<List<DailyUsageStats>> GetDailyStatsAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<List<AgentTypeUsageStats>> GetUsageByAgentTypeAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<PlatformUsageStats> GetPlatformStatsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    Task<List<TopPluginStats>> GetTopPluginsByInstallsAsync(
        DateTime startDate,
        DateTime endDate,
        int topN,
        CancellationToken cancellationToken = default);

    Task<List<TopPluginStats>> GetTopPluginsByInvocationsAsync(
        DateTime startDate,
        DateTime endDate,
        int topN,
        CancellationToken cancellationToken = default);
}

public record DailyUsageStats(
    DateTime Date,
    int Invocations,
    int Errors,
    int ActiveUsers);

public record AgentTypeUsageStats(
    string AgentType,
    int Invocations,
    int Errors,
    int UniqueAgents);

public record PlatformUsageStats(
    int TotalPlugins,
    int TotalInstalls,
    int TotalUninstalls,
    int TotalInvocations,
    int TotalErrors,
    int PlatformActiveUsers);

public record TopPluginStats(
    Guid PluginId,
    string PluginName,
    int Value);
