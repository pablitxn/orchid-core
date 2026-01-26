using Domain.Entities;

namespace Application.UseCases.AdminBilling.Common;

/// <summary>
/// DTO for webhook event logs.
/// </summary>
public sealed record WebhookLogDto
{
    public Guid Id { get; init; }
    public string StripeEventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public WebhookEventStatus Status { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
    public DateTime? NextRetryAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ProcessedAt { get; init; }
    public bool CanRetry { get; init; }

    public static WebhookLogDto FromEntity(StripeWebhookEventEntity entity, int maxRetries = 5) => new()
    {
        Id = entity.Id,
        StripeEventId = entity.StripeEventId,
        EventType = entity.EventType,
        Status = entity.Status,
        ErrorMessage = entity.ErrorMessage,
        RetryCount = entity.RetryCount,
        NextRetryAt = entity.NextRetryAt,
        CreatedAt = entity.CreatedAt,
        ProcessedAt = entity.ProcessedAt,
        CanRetry = entity.CanRetry(maxRetries)
    };
}
