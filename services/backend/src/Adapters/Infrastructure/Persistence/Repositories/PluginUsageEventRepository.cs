using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginUsageEventRepository : IPluginUsageEventRepository
{
    private readonly ApplicationDbContext _context;

    public PluginUsageEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PluginUsageEventEntity> CreateAsync(PluginUsageEventEntity entity, CancellationToken cancellationToken = default)
    {
        _context.PluginUsageEvents.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task<PluginUsageEventEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PluginUsageEvents
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<List<PluginUsageEventEntity>> GetByPluginIdAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.PluginUsageEvents
            .Where(e => e.PluginId == pluginId && e.OccurredAt >= startDate && e.OccurredAt <= endDate)
            .OrderByDescending(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountByTypeAsync(
        Guid pluginId,
        string eventType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.PluginUsageEvents
            .CountAsync(e =>
                e.PluginId == pluginId &&
                e.EventType == eventType &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate,
                cancellationToken);
    }

    public async Task<int> CountDistinctUsersAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        return await _context.PluginUsageEvents
            .Where(e =>
                e.PluginId == pluginId &&
                e.UserId != null &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .Select(e => e.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<double> GetErrorRateAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var invocations = await _context.PluginUsageEvents
            .CountAsync(e =>
                e.PluginId == pluginId &&
                e.EventType == "invoked" &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate,
                cancellationToken);

        if (invocations == 0)
            return 0.0;

        var errors = await _context.PluginUsageEvents
            .CountAsync(e =>
                e.PluginId == pluginId &&
                e.EventType == "error" &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate,
                cancellationToken);

        return (double)errors / invocations;
    }

    public async Task<Dictionary<string, int>> GetEventCountsByTypeAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var counts = await _context.PluginUsageEvents
            .Where(e =>
                e.PluginId == pluginId &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .GroupBy(e => e.EventType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(x => x.Type, x => x.Count);
    }

    public async Task<List<DailyUsageStats>> GetDailyStatsAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var events = await _context.PluginUsageEvents
            .Where(e =>
                e.PluginId == pluginId &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .ToListAsync(cancellationToken);

        var dailyStats = events
            .GroupBy(e => e.OccurredAt.Date)
            .Select(g => new DailyUsageStats(
                g.Key,
                g.Count(e => e.EventType == "invoked"),
                g.Count(e => e.EventType == "error"),
                g.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count()))
            .OrderByDescending(d => d.Date)
            .ToList();

        return dailyStats;
    }

    public async Task<List<AgentTypeUsageStats>> GetUsageByAgentTypeAsync(
        Guid pluginId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var events = await _context.PluginUsageEvents
            .Where(e =>
                e.PluginId == pluginId &&
                e.AgentId != null &&
                (e.EventType == "invoked" || e.EventType == "error") &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .Include(e => e.Agent)
            .ToListAsync(cancellationToken);

        var agentTypeStats = events
            .GroupBy(e => e.Agent?.Personality ?? "unknown")
            .Select(g => new AgentTypeUsageStats(
                g.Key,
                g.Count(e => e.EventType == "invoked"),
                g.Count(e => e.EventType == "error"),
                g.Select(e => e.AgentId).Distinct().Count()))
            .ToList();

        return agentTypeStats;
    }

    public async Task<PlatformUsageStats> GetPlatformStatsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var events = await _context.PluginUsageEvents
            .Where(e => e.OccurredAt >= startDate && e.OccurredAt <= endDate)
            .ToListAsync(cancellationToken);

        var totalPlugins = events.Select(e => e.PluginId).Distinct().Count();
        var totalInstalls = events.Count(e => e.EventType == "installed");
        var totalUninstalls = events.Count(e => e.EventType == "uninstalled");
        var totalInvocations = events.Count(e => e.EventType == "invoked");
        var totalErrors = events.Count(e => e.EventType == "error");
        var platformActiveUsers = events.Where(e => e.UserId.HasValue).Select(e => e.UserId).Distinct().Count();

        return new PlatformUsageStats(
            totalPlugins,
            totalInstalls,
            totalUninstalls,
            totalInvocations,
            totalErrors,
            platformActiveUsers);
    }

    public async Task<List<TopPluginStats>> GetTopPluginsByInstallsAsync(
        DateTime startDate,
        DateTime endDate,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var topPlugins = await _context.PluginUsageEvents
            .Where(e =>
                e.EventType == "installed" &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .GroupBy(e => e.PluginId)
            .Select(g => new { PluginId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var pluginIds = topPlugins.Select(p => p.PluginId).ToList();
        var plugins = await _context.Plugins
            .Where(p => pluginIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return topPlugins
            .Select(p => new TopPluginStats(
                p.PluginId,
                plugins.GetValueOrDefault(p.PluginId, "Unknown"),
                p.Count))
            .ToList();
    }

    public async Task<List<TopPluginStats>> GetTopPluginsByInvocationsAsync(
        DateTime startDate,
        DateTime endDate,
        int topN,
        CancellationToken cancellationToken = default)
    {
        var topPlugins = await _context.PluginUsageEvents
            .Where(e =>
                e.EventType == "invoked" &&
                e.OccurredAt >= startDate &&
                e.OccurredAt <= endDate)
            .GroupBy(e => e.PluginId)
            .Select(g => new { PluginId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(topN)
            .ToListAsync(cancellationToken);

        var pluginIds = topPlugins.Select(p => p.PluginId).ToList();
        var plugins = await _context.Plugins
            .Where(p => pluginIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return topPlugins
            .Select(p => new TopPluginStats(
                p.PluginId,
                plugins.GetValueOrDefault(p.PluginId, "Unknown"),
                p.Count))
            .ToList();
    }
}
