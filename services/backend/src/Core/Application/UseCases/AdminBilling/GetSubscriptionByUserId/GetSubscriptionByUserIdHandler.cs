using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.GetSubscriptionByUserId;

public class GetSubscriptionByUserIdHandler(
    ISubscriptionRepository subscriptionRepository,
    ILogger<GetSubscriptionByUserIdHandler> logger)
    : IRequestHandler<GetSubscriptionByUserIdQuery, AdminSubscriptionDto>
{
    public async Task<AdminSubscriptionDto> Handle(
        GetSubscriptionByUserIdQuery request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Admin fetching subscription for user {UserId}",
            request.UserId);

        var subscription = await subscriptionRepository.GetByUserIdWithPlanAsync(
            request.UserId,
            cancellationToken);

        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        return AdminSubscriptionDto.FromEntity(subscription);
    }
}
