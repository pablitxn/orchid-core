using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.GetAllSubscriptions;

public class GetAllSubscriptionsHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository,
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

        var dtos = new List<AdminSubscriptionDto>();
        foreach (var subscription in items)
        {
            var userCredits = await userCreditsRepository.GetByUserIdAsync(subscription.UserId, cancellationToken)
                ?? new Domain.Entities.UserCreditsEntity
                {
                    Id = Guid.Empty,
                    UserId = subscription.UserId,
                    AvailableCredits = 0,
                    TotalCreditsGranted = 0,
                    TotalCreditsConsumed = 0
                };

            dtos.Add(AdminSubscriptionDto.FromEntityWithCredits(subscription, userCredits));
        }

        return new GetAllSubscriptionsDto
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
