using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.GetSubscription;

public class GetSubscriptionHandler(ISubscriptionRepository repository)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionEntity?>
{
    private readonly ISubscriptionRepository _repository = repository;

    public async Task<SubscriptionEntity?> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByUserIdAsync(request.UserId, cancellationToken);
    }
}