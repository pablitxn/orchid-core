using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Subscription.UpdateAutoRenew;

public class UpdateAutoRenewHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository,
    IEventPublisher eventPublisher) : IRequestHandler<UpdateAutoRenewCommand, SubscriptionDto>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;
    private readonly IUserCreditsRepository _userCredits = userCreditsRepository;

    public async Task<SubscriptionDto> Handle(UpdateAutoRenewCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription == null)
            throw new SubscriptionNotFoundException(request.UserId);

        subscription.AutoRenew = request.AutoRenew;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _subs.UpdateAsync(subscription, cancellationToken);
        await _events.PublishAsync(new SubscriptionUpdatedEvent(subscription.Id, subscription.UserId));
        var userCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken)
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
