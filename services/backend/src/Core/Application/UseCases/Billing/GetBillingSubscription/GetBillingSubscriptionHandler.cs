using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingSubscription;

public sealed class GetBillingSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository)
    : IRequestHandler<GetBillingSubscriptionQuery, SubscriptionDto?>
{
    public async Task<SubscriptionDto?> Handle(
        GetBillingSubscriptionQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        return subscription is null ? null : SubscriptionDto.FromEntity(subscription);
    }
}
