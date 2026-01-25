using Domain.Entities;

namespace Application.Interfaces;

/// <summary>
/// Repository for Stripe webhook events.
/// </summary>
public interface IStripeWebhookEventRepository
{
    /// <summary>
    /// Checks if an event has already been processed (for idempotency).
    /// </summary>
    Task<bool> ExistsAsync(string stripeEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an event by its Stripe event ID.
    /// </summary>
    Task<StripeWebhookEventEntity?> GetByStripeEventIdAsync(string stripeEventId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new webhook event record.
    /// </summary>
    Task<StripeWebhookEventEntity> CreateAsync(StripeWebhookEventEntity webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing webhook event record.
    /// </summary>
    Task UpdateAsync(StripeWebhookEventEntity webhookEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed events that can be retried.
    /// </summary>
    Task<IEnumerable<StripeWebhookEventEntity>> GetRetryableEventsAsync(int maxRetries = 3, int limit = 100, CancellationToken cancellationToken = default);
}
