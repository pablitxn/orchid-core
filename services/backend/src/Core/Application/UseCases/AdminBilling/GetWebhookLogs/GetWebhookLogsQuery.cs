using Application.UseCases.AdminBilling.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.AdminBilling.GetWebhookLogs;

/// <summary>
/// Query to get webhook processing logs with filters and pagination.
/// </summary>
public record GetWebhookLogsQuery(
    WebhookEventStatus? Status = null,
    string? EventType = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    int Page = 1,
    int PageSize = 50
) : IRequest<GetWebhookLogsDto>;

/// <summary>
/// Response DTO with paginated webhook logs and statistics.
/// </summary>
public sealed record GetWebhookLogsDto
{
    public IReadOnlyList<WebhookLogDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public WebhookStatsDto Stats { get; init; } = new();
}

/// <summary>
/// Webhook processing statistics.
/// </summary>
public sealed record WebhookStatsDto
{
    public int TotalEvents { get; init; }
    public int ProcessedEvents { get; init; }
    public int FailedEvents { get; init; }
    public int DeadLetterEvents { get; init; }
    public int PendingRetryEvents { get; init; }
}
