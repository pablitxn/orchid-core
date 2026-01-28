using Domain.Entities;

namespace Application.Interfaces;

public interface IWorkspaceSubscriptionRepository
{
    Task<WorkspaceSubscriptionEntity?> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken);

    Task<WorkspaceSubscriptionEntity?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken);

    Task<WorkspaceSubscriptionEntity> CreateAsync(WorkspaceSubscriptionEntity subscription, CancellationToken cancellationToken);

    Task UpdateAsync(WorkspaceSubscriptionEntity subscription, CancellationToken cancellationToken);
}
