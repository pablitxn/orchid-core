using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.GetAllSubscriptions;

public class GetAllSubscriptionsHandler(
    ISubscriptionRepository subscriptionRepository,
    ILogger<GetAllSubscriptionsHandler> logger)
    : IRequestHandler<GetAllSubscriptionsQuery, GetAllSubscriptionsDto>
{
    public async Task<GetAllSubscriptionsDto> Handle(
        GetAllSubscriptionsQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Admin fetching all subscriptions. Page: {Page}, PageSize: {PageSize}",
            request.Page, request.PageSize);

        var (items, totalCount) = await subscriptionRepository.GetAllAsync(
            request.Page,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(AdminSubscriptionDto.FromEntity).ToList();

        return new GetAllSubscriptionsDto
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
