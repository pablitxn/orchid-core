using Application.UseCases.AdminBilling.Common;
using MediatR;

namespace Application.UseCases.AdminBilling.RetryWebhook;

/// <summary>
/// Command to manually retry a failed webhook event.
/// </summary>
public record RetryWebhookCommand(
    Guid WebhookEventId,
    Guid AdminUserId
) : IRequest<RetryWebhookDto>;

/// <summary>
/// Result of the webhook retry operation.
/// </summary>
public sealed record RetryWebhookDto
{
    public Guid Id { get; init; }
    public string StripeEventId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Message { get; init; }
    public WebhookLogDto WebhookLog { get; init; } = null!;
}
