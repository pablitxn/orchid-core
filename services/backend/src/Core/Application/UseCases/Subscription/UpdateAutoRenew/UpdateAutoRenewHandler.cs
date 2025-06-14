using Application.Interfaces;
using Application.UseCases.Subscription.ConsumeCredits;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Subscription.UpdateAutoRenew;

public class UpdateAutoRenewHandler(
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher) : IRequestHandler<UpdateAutoRenewCommand, SubscriptionEntity>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;

    public async Task<SubscriptionEntity> Handle(UpdateAutoRenewCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription == null)
            throw new SubscriptionNotFoundException(request.UserId);

        subscription.AutoRenew = request.AutoRenew;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subs.UpdateAsync(subscription, cancellationToken);
        await _events.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
        return subscription;
    }
}