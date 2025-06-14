using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SubscriptionPlanRepository(ApplicationDbContext db) : ISubscriptionPlanRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<SubscriptionPlanEntity?> GetByPlanAsync(SubscriptionPlanEnum planEnum,
        CancellationToken cancellationToken = default)
    {
        return await _db.SubscriptionPlans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PlanEnum == planEnum, cancellationToken);
    }

    public async Task<SubscriptionPlanEntity> CreateAsync(SubscriptionPlanEntity plan,
        CancellationToken cancellationToken = default)
    {
        _db.SubscriptionPlans.Add(plan);
        await _db.SaveChangesAsync(cancellationToken);
        return plan;
    }

    public async Task UpdateAsync(SubscriptionPlanEntity plan, CancellationToken cancellationToken = default)
    {
        _db.SubscriptionPlans.Update(plan);
        await _db.SaveChangesAsync(cancellationToken);
    }
}