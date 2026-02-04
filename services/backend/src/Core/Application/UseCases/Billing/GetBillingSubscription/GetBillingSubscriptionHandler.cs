using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingSubscription;

public sealed class GetBillingSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository)
    : IRequestHandler<GetBillingSubscriptionQuery, SubscriptionDto?>
{
    public async Task<SubscriptionDto?> Handle(
        GetBillingSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
            return null;

        var userCredits = await userCreditsRepository.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? new Domain.Entities.UserCreditsEntity
            {
                Id = Guid.Empty,
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0
            };

        return SubscriptionDto.FromEntityWithCredits(subscription, userCredits);
    }
}
