using System.Transactions;
using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Subscription.CreateSubscription;

public class CreateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<CreateSubscriptionCommand, SubscriptionDto>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;
    private readonly IUserCreditsRepository _userCredits = userCreditsRepository;
    private readonly IUserRepository _users = userRepository;

    public async Task<SubscriptionDto> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId);
        if (user == null)
            throw new UserNotFoundException(request.UserId);

        // Input validation
        if (request.Credits < 0)
            throw new ValidationException("Credits must be non-negative", nameof(request.Credits));
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.UtcNow)
            throw new ValidationException("Expiration date must be in the future", nameof(request.ExpiresAt));

        // Prevent duplicate active subscription
        var existing = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existing != null && (existing.ExpiresAt == null || existing.ExpiresAt > DateTime.UtcNow))
            throw new AlreadySubscribedException(request.UserId);

        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt
        };

        // Get or create UserCreditsEntity
        var userCredits = await _userCredits.GetByUserIdAsync(request.UserId, cancellationToken);
        if (userCredits == null)
        {
            userCredits = new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                AvailableCredits = request.Credits,
                TotalCreditsGranted = request.Credits,
                TotalCreditsConsumed = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _userCredits.CreateAsync(userCredits, cancellationToken);
        }
        else
        {
            // Add the credits to existing balance
            userCredits.GrantCredits(request.Credits);
            await _userCredits.UpdateAsync(userCredits, cancellationToken);
        }

        // Ensure creation and event publish are atomic
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await _subs.CreateAsync(sub, cancellationToken);
        await _events.PublishAsync(new SubscriptionCreatedEvent(sub.Id, sub.UserId));
        scope.Complete();

        return SubscriptionDto.FromEntityWithCredits(sub, userCredits);
    }
}