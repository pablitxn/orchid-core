using Application.Interfaces;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Subscription.AddCredits;

public class AddCreditsHandler(
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<AddCreditsCommand, SubscriptionEntity>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;

    public async Task<SubscriptionEntity> Handle(AddCreditsCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription == null)
            throw new SubscriptionNotFoundException(request.UserId);

        subscription.AddCredits(request.Amount);

        await _subs.UpdateAsync(subscription, cancellationToken);
        await _events.PublishAsync(new CreditsAddedEvent(subscription.Id, subscription.UserId, request.Amount));
        return subscription;
    }
}