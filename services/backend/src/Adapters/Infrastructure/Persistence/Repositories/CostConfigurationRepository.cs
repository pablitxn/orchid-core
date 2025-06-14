using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class CostConfigurationRepository : ICostConfigurationRepository
{
    private readonly ApplicationDbContext _context;

    public CostConfigurationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CostConfigurationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CostConfigurations
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<CostConfigurationEntity?> GetCostForTypeAsync(string costType, Guid? resourceId = null, CancellationToken cancellationToken = default)
    {
        return await _context.CostConfigurations
            .Where(c => c.CostType == costType && c.ResourceId == resourceId && c.IsActive)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<CostConfigurationEntity>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CostConfigurations
            .Where(c => c.IsActive)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CostConfigurationEntity>> GetByResourceIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        return await _context.CostConfigurations
            .Where(c => c.ResourceId == resourceId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CostConfigurationEntity> CreateAsync(CostConfigurationEntity configuration, CancellationToken cancellationToken = default)
    {
        _context.CostConfigurations.Add(configuration);
        await _context.SaveChangesAsync(cancellationToken);
        return configuration;
    }

    public async Task UpdateAsync(CostConfigurationEntity configuration, CancellationToken cancellationToken = default)
    {
        _context.CostConfigurations.Update(configuration);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var configuration = await GetByIdAsync(id, cancellationToken);
        if (configuration != null)
        {
            _context.CostConfigurations.Remove(configuration);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}