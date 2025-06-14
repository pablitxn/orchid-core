using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserPluginRepository(ApplicationDbContext dbContext) : IUserPluginRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<UserPluginEntity?> GetByUserAndPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserPlugins
            .Include(up => up.Plugin)
            .FirstOrDefaultAsync(up => up.UserId == userId && up.PluginId == pluginId, cancellationToken);
    }

    public async Task<List<UserPluginEntity>> ListByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserPlugins
            .Include(up => up.Plugin)
            .Where(up => up.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserOwnsPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken)
    {
        return await _dbContext.UserPlugins
            .AnyAsync(up => up.UserId == userId && up.PluginId == pluginId && up.IsActive, cancellationToken);
    }

    public async Task CreateAsync(UserPluginEntity userPlugin, CancellationToken cancellationToken)
    {
        _dbContext.UserPlugins.Add(userPlugin);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserPluginEntity userPlugin, CancellationToken cancellationToken)
    {
        _dbContext.UserPlugins.Update(userPlugin);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<UserPluginEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await ListByUserAsync(userId, cancellationToken);
    }

    public async Task DeleteByUserAndPluginAsync(Guid userId, Guid pluginId, CancellationToken cancellationToken)
    {
        var userPlugin = await GetByUserAndPluginAsync(userId, pluginId, cancellationToken);
        if (userPlugin != null)
        {
            _dbContext.UserPlugins.Remove(userPlugin);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}