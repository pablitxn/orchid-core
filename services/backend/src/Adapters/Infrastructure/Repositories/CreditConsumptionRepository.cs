using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public sealed class CreditConsumptionRepository(ApplicationDbContext context) : ICreditConsumptionRepository
{
    public async Task<CreditConsumptionEntity> CreateAsync(CreditConsumptionEntity creditConsumption, CancellationToken cancellationToken = default)
    {
        context.CreditConsumptions.Add(creditConsumption);
        await context.SaveChangesAsync(cancellationToken);
        return creditConsumption;
    }

    public async Task<CreditConsumptionEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.CreditConsumptions
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.CreditConsumptions
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetRecentByUserIdAsync(Guid userId, int days, CancellationToken cancellationToken = default)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        
        return await context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumedAt >= startDate)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CreditConsumptionEntity>> GetByDateRangeAsync(Guid userId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await context.CreditConsumptions
            .Where(c => c.UserId == userId && c.ConsumedAt >= startDate && c.ConsumedAt <= endDate)
            .OrderByDescending(c => c.ConsumedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalConsumedByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.CreditConsumptions
            .Where(c => c.UserId == userId)
            .SumAsync(c => c.CreditsConsumed, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetConsumptionByTypeAsync(Guid userId, DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = context.CreditConsumptions.Where(c => c.UserId == userId);

        if (startDate.HasValue)
        {
            query = query.Where(c => c.ConsumedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(c => c.ConsumedAt <= endDate.Value);
        }

        return await query
            .GroupBy(c => c.ConsumptionType)
            .Select(g => new { Type = g.Key, Total = g.Sum(c => c.CreditsConsumed) })
            .ToDictionaryAsync(x => x.Type, x => x.Total, cancellationToken);
    }
}