using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class CreditConsumptionRepository : ICreditConsumptionRepository
{
    private readonly ApplicationDbContext _context;

    public CreditConsumptionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<CreditConsumptionEntity> CreateAsync(CreditConsumptionEntity consumption, CancellationToken cancellationToken = default)
    {
        _context.CreditConsumptions.Add(consumption);
        await _context.SaveChangesAsync(cancellationToken);
        return consumption;
    }

    public async Task<CreditConsumptionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CreditConsumptions
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CreditConsumptions
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetRecentByUserIdAsync(Guid userId, int days, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        return await _context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumedAt >= since)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumedAt >= startDate && c.ConsumedAt <= endDate)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalConsumedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.CreditConsumptions
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.CreditsConsumed, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetConsumptionByTypeAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _context.CreditConsumptions
            .Where(c => c.UserId == userId);

        if (startDate.HasValue)
            query = query.Where(c => c.ConsumedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(c => c.ConsumedAt <= endDate.Value);

        var consumptionByType = await query
            .GroupBy(c => c.ConsumptionType)
            .Select(g => new { Type = g.Key, Total = g.Sum(c => c.CreditsConsumed) })
            .ToListAsync(cancellationToken);

        return consumptionByType.ToDictionary(x => x.Type, x => x.Total);
    }
}