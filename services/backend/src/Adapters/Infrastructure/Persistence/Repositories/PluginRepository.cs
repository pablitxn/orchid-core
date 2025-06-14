using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class PluginRepository(ApplicationDbContext dbContext) : IPluginRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<PluginEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.Plugins.FindAsync([id], cancellationToken);
    }

    public async Task<PluginEntity?> GetBySystemNameAsync(string systemName, CancellationToken cancellationToken)
    {
        return await _dbContext.Plugins
            .FirstOrDefaultAsync(p => p.SystemName == systemName, cancellationToken);
    }

    public async Task<List<PluginEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Plugins.ToListAsync(cancellationToken);
    }

    public async Task<List<PluginEntity>> ListActiveAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Plugins
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<PluginEntity>> ListByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserPlugins
            .Where(up => up.UserId == userId && up.IsActive)
            .Include(up => up.Plugin)
            .Select(up => up.Plugin)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(PluginEntity plugin, CancellationToken cancellationToken)
    {
        _dbContext.Plugins.Add(plugin);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PluginEntity plugin, CancellationToken cancellationToken)
    {
        _dbContext.Plugins.Update(plugin);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var plugin = await _dbContext.Plugins.FindAsync([id], cancellationToken);
        if (plugin is not null)
        {
            _dbContext.Plugins.Remove(plugin);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<List<PluginEntity>> ListByAgentAsync(Guid agentId, CancellationToken cancellationToken)
    {
        // First get the agent to retrieve plugin IDs
        var agent = await _dbContext.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken);
        
        if (agent == null || agent.PluginIds == null || agent.PluginIds.Length == 0)
        {
            return new List<PluginEntity>();
        }

        // Then get all plugins that match the agent's plugin IDs
        return await _dbContext.Plugins
            .Where(p => agent.PluginIds.Contains(p.Id) && p.IsActive)
            .ToListAsync(cancellationToken);
    }
}