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
    /// Gets an event by its ID.
    /// </summary>
    Task<StripeWebhookEventEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Gets failed events that are due for retry (NextRetryAt <= now).
    /// </summary>
    Task<IEnumerable<StripeWebhookEventEntity>> GetEventsReadyForRetryAsync(int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets events that have been moved to the dead letter queue.
    /// </summary>
    Task<IEnumerable<StripeWebhookEventEntity>> GetDeadLetterEventsAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets webhook event statistics for monitoring.
    /// </summary>
    Task<WebhookEventStats> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets webhook events with pagination and optional filters for admin purposes.
    /// </summary>
    Task<(IEnumerable<StripeWebhookEventEntity> Items, int TotalCount)> GetAllAsync(
        WebhookEventStatus? status = null,
        string? eventType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics about webhook event processing.
/// </summary>
public record WebhookEventStats(
    int TotalEvents,
    int ProcessedEvents,
    int FailedEvents,
    int DeadLetterEvents,
    int PendingRetryEvents);
