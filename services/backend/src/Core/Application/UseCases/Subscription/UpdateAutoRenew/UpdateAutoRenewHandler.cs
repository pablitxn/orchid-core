using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Subscription.UpdateAutoRenew;

public class UpdateAutoRenewHandler(
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher) : IRequestHandler<UpdateAutoRenewCommand, SubscriptionDto>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;

    public async Task<SubscriptionDto> Handle(UpdateAutoRenewCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription == null)
            throw new SubscriptionNotFoundException(request.UserId);

        subscription.AutoRenew = request.AutoRenew;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subs.UpdateAsync(subscription, cancellationToken);
        await _events.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
        return SubscriptionDto.FromEntity(subscription);
    }
}