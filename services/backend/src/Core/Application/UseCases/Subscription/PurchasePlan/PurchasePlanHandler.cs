using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Subscription.PurchasePlan;

public class PurchasePlanHandler(
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository subscriptionPlanRepository,
    IUserCreditsRepository userCreditsRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<PurchasePlanCommand, SubscriptionDto>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;
    private readonly ISubscriptionPlanRepository _plans = subscriptionPlanRepository;
    private readonly IUserCreditsRepository _userCredits = userCreditsRepository;

    public async Task<SubscriptionDto> Handle(PurchasePlanCommand request, CancellationToken cancellationToken)
    {
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        var plan = await _plans.GetByPlanAsync(request.PlanEnum, cancellationToken);

        if (plan == null)
        {
            throw new SubscriptionPlanNotFoundException(request.PlanEnum.ToString());
        }

        var isUnlimited = request.PlanEnum == SubscriptionPlanEnum.Unlimited;
        var credits = isUnlimited ? 0 : plan.Credits;

        if (subscription == null)
        {
            subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = request.PlanEnum == SubscriptionPlanEnum.Monthly100 ? DateTime.UtcNow.AddMonths(1) : null,
                SubscriptionPlanId = plan.Id
            };
            await _subs.CreateAsync(subscription, cancellationToken);
            await _events.PublishAsync(new SubscriptionCreatedEvent(subscription.Id, subscription.UserId));
        }
        else
        {
            if (request.PlanEnum == SubscriptionPlanEnum.Monthly100)
                subscription.ExpiresAt = DateTime.UtcNow.AddMonths(1);
            subscription.SubscriptionPlanId = plan.Id;
            await _subs.UpdateAsync(subscription, cancellationToken);
        }

        if (!isUnlimited)
        {
            var userCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken);
            if (userCredits == null)
            {
                userCredits = new UserCreditsEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    AvailableCredits = credits,
                    TotalCreditsGranted = credits,
                    TotalCreditsConsumed = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _userCredits.CreateAsync(userCredits, cancellationToken);
            }
            else
            {
                userCredits.GrantCredits(credits);
                await _userCredits.UpdateAsync(userCredits, cancellationToken);
            }

            await _events.PublishAsync(new CreditsAddedEvent(userCredits.Id, userCredits.UserId, credits));
        }

        // Reload to get SubscriptionPlan for DTO
        subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        var finalUserCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken)
            ?? new UserCreditsEntity
            {
                Id = Guid.Empty,
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0
            };

        return SubscriptionDto.FromEntityWithCredits(subscription!, finalUserCredits);
    }
}
