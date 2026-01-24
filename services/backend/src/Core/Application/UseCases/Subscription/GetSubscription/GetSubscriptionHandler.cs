using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.GetSubscription;

public class GetSubscriptionHandler(ISubscriptionRepository repository)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionDto?>
{
    private readonly ISubscriptionRepository _repository = repository;

    public async Task<SubscriptionDto?> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
        return subscription is null ? null : SubscriptionDto.FromEntity(subscription);
    }
}