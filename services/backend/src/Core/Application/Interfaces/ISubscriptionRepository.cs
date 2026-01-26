using Domain.Entities;

namespace Application.Interfaces;

public interface ISubscriptionRepository
{
    Task<SubscriptionEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<SubscriptionEntity?> GetByStripeCustomerIdAsync(string stripeCustomerId, CancellationToken cancellationToken = default);

    Task<SubscriptionEntity?> GetByStripeSubscriptionIdAsync(string stripeSubscriptionId, CancellationToken cancellationToken = default);

    Task<SubscriptionEntity> CreateAsync(SubscriptionEntity subscription,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(SubscriptionEntity subscription, CancellationToken cancellationToken = default);

    Task UpdateWithVersionCheckAsync(SubscriptionEntity subscription, int expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions with pagination for admin purposes.
    /// </summary>
    Task<(IEnumerable<SubscriptionEntity> Items, int TotalCount)> GetAllAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a subscription by user ID with plan details loaded for admin purposes.
    /// </summary>
    Task<SubscriptionEntity?> GetByUserIdWithPlanAsync(Guid userId, CancellationToken cancellationToken = default);
}