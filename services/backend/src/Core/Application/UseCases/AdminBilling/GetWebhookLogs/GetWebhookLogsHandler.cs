using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.GetWebhookLogs;

public class GetWebhookLogsHandler(
    IStripeWebhookEventRepository webhookEventRepository,
    ILogger<GetWebhookLogsHandler> logger)
    : IRequestHandler<GetWebhookLogsQuery, GetWebhookLogsDto>
{
    public async Task<GetWebhookLogsDto> Handle(
        GetWebhookLogsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Admin fetching webhook logs. Status: {Status}, EventType: {EventType}, Page: {Page}",
            request.Status, request.EventType, request.Page);

        var (items, totalCount) = await webhookEventRepository.GetAllAsync(
            request.Status,
            request.EventType,
            request.StartDate,
            request.EndDate,
            request.Page,
            request.PageSize,
            cancellationToken);

        var stats = await webhookEventRepository.GetStatsAsync(cancellationToken);

        var dtos = items.Select(WebhookLogDto.FromEntity).ToList();

        return new GetWebhookLogsDto
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
            Stats = new WebhookStatsDto
            {
                TotalEvents = stats.TotalEvents,
                ProcessedEvents = stats.ProcessedEvents,
                FailedEvents = stats.FailedEvents,
                DeadLetterEvents = stats.DeadLetterEvents,
                PendingRetryEvents = stats.PendingRetryEvents
            }
        };
    }
}
