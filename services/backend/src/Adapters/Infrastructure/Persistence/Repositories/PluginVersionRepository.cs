using Application.Interfaces;
using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginVersionRepository(ApplicationDbContext dbContext) : IPluginVersionRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<PluginVersionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginVersions
            .Include(v => v.Plugin)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<PluginVersionEntity?> GetByPluginAndVersionAsync(Guid pluginId, string version, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginVersions
            .Include(v => v.Plugin)
            .FirstOrDefaultAsync(v => v.PluginId == pluginId && v.Version == version, cancellationToken);
    }

    public async Task<PluginVersionEntity?> GetLatestAsync(Guid pluginId, bool includePreRelease = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.PluginVersions
            .Where(v => v.PluginId == pluginId && v.IsActive);

        if (!includePreRelease)
        {
            // Filter out pre-release versions (those containing '-')
            query = query.Where(v => !v.Version.Contains('-'));
        }

        // Get the version marked as latest, or fallback to most recently published
        var latest = await query
            .Where(v => v.IsLatest)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest != null)
            return latest;

        // Fallback: get the most recently published
        return await query
            .OrderByDescending(v => v.PublishedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<PluginVersionEntity>> ListByPluginAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        var versions = await _dbContext.PluginVersions
            .Where(v => v.PluginId == pluginId && v.IsActive)
            .ToListAsync(cancellationToken);

        // Sort by semantic version descending (newest first)
        return versions
            .Select(v => (Entity: v, SemVer: SemanticVersion.TryParse(v.Version, out var sv) ? sv : null))
            .OrderByDescending(x => x.SemVer)
            .Select(x => x.Entity)
            .ToList();
    }

    public async Task<List<PluginVersionEntity>> ListCompatibleVersionsAsync(Guid pluginId, string currentVersion, CancellationToken cancellationToken = default)
    {
        if (!SemanticVersion.TryParse(currentVersion, out var current) || current is null)
        {
            return new List<PluginVersionEntity>();
        }

        var allVersions = await _dbContext.PluginVersions
            .Where(v => v.PluginId == pluginId && v.IsActive)
            .ToListAsync(cancellationToken);

        // Filter to compatible versions (same major) that are older than current
        return allVersions
            .Select(v => (Entity: v, SemVer: SemanticVersion.TryParse(v.Version, out var sv) ? sv : null))
            .Where(x => x.SemVer is not null
                && x.SemVer.Major == current.Major
                && x.SemVer.CompareTo(current) < 0)
            .OrderByDescending(x => x.SemVer)
            .Select(x => x.Entity)
            .ToList();
    }

    public async Task<bool> ExistsAsync(Guid pluginId, string version, CancellationToken cancellationToken = default)
    {
        return await _dbContext.PluginVersions
            .AnyAsync(v => v.PluginId == pluginId && v.Version == version, cancellationToken);
    }

    public async Task CreateAsync(PluginVersionEntity version, CancellationToken cancellationToken = default)
    {
        _dbContext.PluginVersions.Add(version);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PluginVersionEntity version, CancellationToken cancellationToken = default)
    {
        _dbContext.PluginVersions.Update(version);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var version = await _dbContext.PluginVersions.FindAsync([id], cancellationToken);
        if (version is not null)
        {
            _dbContext.PluginVersions.Remove(version);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
