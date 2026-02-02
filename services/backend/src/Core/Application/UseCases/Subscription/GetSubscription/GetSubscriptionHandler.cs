using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.GetSubscription;

public class GetSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    private readonly ISubscriptionRepository _subscriptions = subscriptionRepository;
    private readonly IUserCreditsRepository _userCredits = userCreditsRepository;

    public async Task<SubscriptionDto?> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _subscriptions.GetByUserIdAsync(request.UserId, cancellationToken);
        var userCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken);

        // If no subscription and no credits, return null
        if (subscription is null && userCredits is null)
            return null;

        // If only credits exist (no subscription), return a minimal DTO with credits
        if (subscription is null && userCredits is not null)
            return SubscriptionDto.FromEntityWithCredits(null, userCredits);

        // If subscription exists, get credits (or create with 0)
        userCredits ??= new Domain.Entities.UserCreditsEntity
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
