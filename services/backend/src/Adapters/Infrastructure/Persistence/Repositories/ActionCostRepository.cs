using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ActionCostRepository(ApplicationDbContext db) : IActionCostRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<ActionCostEntity?> GetByActionAsync(string actionType,
        CancellationToken cancellationToken)
    {
        return await _db.ActionCosts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ActionName == actionType, cancellationToken);
    }

    public async Task<ActionCostEntity> CreateAsync(ActionCostEntity cost,
        CancellationToken cancellationToken = default)
    {
        _db.ActionCosts.Add(cost);
        await _db.SaveChangesAsync(cancellationToken);
        return cost;
    }

    public async Task UpdateAsync(ActionCostEntity cost, CancellationToken cancellationToken = default)
    {
        _db.ActionCosts.Update(cost);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task RecordActionCostAsync(string actionType, decimal cost, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<decimal> GetTotalCostAsync(string actionType, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<string, decimal>> GetCostBreakdownAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}