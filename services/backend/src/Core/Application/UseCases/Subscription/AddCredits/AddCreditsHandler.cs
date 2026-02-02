using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Subscription.AddCredits;

public class AddCreditsHandler(
    IUserCreditsRepository userCreditsRepository,
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<AddCreditsCommand, SubscriptionDto>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly IUserCreditsRepository _userCredits = userCreditsRepository;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;

    public async Task<SubscriptionDto> Handle(AddCreditsCommand request, CancellationToken cancellationToken)
    {
        // Get or create UserCreditsEntity for this user
        var userCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken);

        if (userCredits == null)
        {
            userCredits = new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _userCredits.CreateAsync(userCredits, cancellationToken);
        }

        // Add credits via domain logic
        userCredits.GrantCredits(request.Amount);

        await _userCredits.UpdateAsync(userCredits, cancellationToken);
        await _events.PublishAsync(new CreditsAddedEvent(userCredits.Id, userCredits.UserId, request.Amount));

        // Get subscription for response (may be null)
        var subscription = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);

        return SubscriptionDto.FromEntityWithCredits(subscription, userCredits);
    }
}
