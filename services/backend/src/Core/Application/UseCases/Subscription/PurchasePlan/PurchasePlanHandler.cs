using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Subscription.PurchasePlan;

public class PurchasePlanHandler(
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<PurchasePlanCommand, SubscriptionEntity>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;

    public async Task<SubscriptionEntity> Handle(PurchasePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        var credits = request.PlanEnum switch
        {
            SubscriptionPlanEnum.Monthly100 => 100,
            SubscriptionPlanEnum.Package5 => 5,
            SubscriptionPlanEnum.Package10 => 10,
            SubscriptionPlanEnum.Package25 => 25,
            _ => throw new ArgumentOutOfRangeException(nameof(request.PlanEnum))
        };

        if (subscription == null)
        {
            subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Credits = credits,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = request.PlanEnum == SubscriptionPlanEnum.Monthly100 ? DateTime.UtcNow.AddMonths(1) : null
            };
            await _subs.CreateAsync(subscription, cancellationToken);
            await _events.PublishAsync(new SubscriptionCreatedEvent(subscription.Id, subscription.UserId));
        }
        else
        {
            subscription.AddCredits(credits);
            if (request.PlanEnum == SubscriptionPlanEnum.Monthly100)
                subscription.ExpiresAt = DateTime.UtcNow.AddMonths(1);
            await _subs.UpdateAsync(subscription, cancellationToken);
        }

        await _events.PublishAsync(new CreditsAddedEvent(subscription.Id, subscription.UserId, credits));
        return subscription;
    }
}