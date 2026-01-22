using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class FeatureFlagRepository(ApplicationDbContext dbContext) : IFeatureFlagRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<FeatureFlagEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags.FindAsync([id], cancellationToken);
    }

    public async Task<FeatureFlagEntity?> GetByKeyAsync(string key, CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .FirstOrDefaultAsync(f => f.Key == key, cancellationToken);
    }

    public async Task<List<FeatureFlagEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags.ToListAsync(cancellationToken);
    }

    public async Task<List<FeatureFlagEntity>> ListEnabledAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.FeatureFlags
            .Where(f => f.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task CreateAsync(FeatureFlagEntity featureFlag, CancellationToken cancellationToken)
    {
        _dbContext.FeatureFlags.Add(featureFlag);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FeatureFlagEntity featureFlag, CancellationToken cancellationToken)
    {
        featureFlag.UpdatedAt = DateTime.UtcNow;
        _dbContext.FeatureFlags.Update(featureFlag);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var featureFlag = await _dbContext.FeatureFlags.FindAsync([id], cancellationToken);
        if (featureFlag is not null)
        {
            _dbContext.FeatureFlags.Remove(featureFlag);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
