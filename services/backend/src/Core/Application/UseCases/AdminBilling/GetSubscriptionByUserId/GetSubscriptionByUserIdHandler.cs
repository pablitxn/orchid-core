using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.GetSubscriptionByUserId;

public class GetSubscriptionByUserIdHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository,
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

        var userCredits = await userCreditsRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? new UserCreditsEntity
            {
                Id = Guid.Empty,
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0
            };

        return AdminSubscriptionDto.FromEntityWithCredits(subscription, userCredits);
    }
}
