using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class WorkspaceSubscriptionRepository(ApplicationDbContext dbContext) : IWorkspaceSubscriptionRepository
{
    private readonly ApplicationDbContext _dbContext = dbContext;

    public async Task<WorkspaceSubscriptionEntity?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceSubscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.WorkspaceId == workspaceId, cancellationToken);
    }

    public async Task<WorkspaceSubscriptionEntity?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken)
    {
        return await _dbContext.WorkspaceSubscriptions
            .Include(s => s.SubscriptionPlan)
            .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSubscriptionId, cancellationToken);
    }

    public async Task<WorkspaceSubscriptionEntity> CreateAsync(WorkspaceSubscriptionEntity subscription, CancellationToken cancellationToken)
    {
        _dbContext.WorkspaceSubscriptions.Add(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task UpdateAsync(WorkspaceSubscriptionEntity subscription, CancellationToken cancellationToken)
    {
        _dbContext.WorkspaceSubscriptions.Update(subscription);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
