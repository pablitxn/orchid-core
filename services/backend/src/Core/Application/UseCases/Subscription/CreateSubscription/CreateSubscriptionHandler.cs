using System.Transactions;
using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;

namespace Application.UseCases.Subscription.CreateSubscription;

public class CreateSubscriptionHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher)
    : IRequestHandler<CreateSubscriptionCommand, SubscriptionEntity>
{
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ISubscriptionRepository _subs = subscriptionRepository;
    private readonly IUserRepository _users = userRepository;

    public async Task<SubscriptionEntity> Handle(CreateSubscriptionCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId);
        if (user == null)
            throw new ArgumentException("User not found", nameof(request.UserId));

        // Input validation
        if (request.Credits < 0)
            throw new ArgumentException("Credits must be non-negative", nameof(request.Credits));
        if (request.ExpiresAt.HasValue && request.ExpiresAt.Value <= DateTime.UtcNow)
            throw new ArgumentException("Expiration date must be in the future", nameof(request.ExpiresAt));

        // Prevent duplicate active subscription
        var existing = await _subs.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existing != null && (existing.ExpiresAt == null || existing.ExpiresAt > DateTime.UtcNow))
            throw new InvalidOperationException("User already has an active subscription.");

        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Credits = request.Credits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresAt
        };

        // Ensure creation and event publish are atomic
        using var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        await _subs.CreateAsync(sub, cancellationToken);
        await _events.PublishAsync(new SubscriptionCreatedEvent(sub.Id, sub.UserId));
        scope.Complete();
        return sub;
    }
}