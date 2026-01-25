namespace Domain.Entities;

/// <summary>
/// Stores Stripe webhook events for idempotent processing and audit trail.
/// </summary>
public class StripeWebhookEventEntity
{
    public Guid Id { get; init; }

    /// <summary>
    /// Stripe event ID (e.g., "evt_xxxxx"). Used for deduplication.
    /// </summary>
    public required string StripeEventId { get; init; }

    /// <summary>
    /// Event type (e.g., "customer.subscription.created").
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Raw JSON payload from Stripe.
    /// </summary>
    public required string Payload { get; init; }

    /// <summary>
    /// Processing status of the event.
    /// </summary>
    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Pending;

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of processing attempts.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// When the next retry should be attempted (for exponential backoff).
    /// </summary>
    public DateTime? NextRetryAt { get; set; }

    /// <summary>
    /// When the event was received.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the event was successfully processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Marks the event as successfully processed.
    /// </summary>
    public void MarkProcessed()
    {
        Status = WebhookEventStatus.Processed;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the event as failed with an error message.
    /// </summary>
    public void MarkFailed(string errorMessage)
    {
        Status = WebhookEventStatus.Failed;
        ErrorMessage = errorMessage;
        RetryCount++;
    }

    /// <summary>
    /// Checks if the event can be retried.
    /// </summary>
    public bool CanRetry(int maxRetries = 3)
    {
        return Status == WebhookEventStatus.Failed && RetryCount < maxRetries;
    }

    /// <summary>
    /// Schedules the event for retry with exponential backoff.
    /// Base delay: 30 seconds, multiplied by 2^retryCount (30s, 60s, 120s, 240s, ...).
    /// </summary>
    public void ScheduleRetry(int maxRetries = 5)
    {
        if (RetryCount >= maxRetries)
        {
            MoveToDeadLetter();
            return;
        }

        Status = WebhookEventStatus.Failed;
        var backoffSeconds = 30 * Math.Pow(2, RetryCount);
        NextRetryAt = DateTime.UtcNow.AddSeconds(backoffSeconds);
    }

    /// <summary>
    /// Prepares the event for retry processing.
    /// </summary>
    public void StartRetryProcessing()
    {
        Status = WebhookEventStatus.Processing;
        NextRetryAt = null;
    }

    /// <summary>
    /// Moves the event to dead letter queue after exhausting retries.
    /// </summary>
    public void MoveToDeadLetter()
    {
        Status = WebhookEventStatus.DeadLetter;
        NextRetryAt = null;
    }
}

/// <summary>
/// Processing status for webhook events.
/// </summary>
public enum WebhookEventStatus
{
    Pending,
    Processing,
    Processed,
    Failed,
    DeadLetter
}
