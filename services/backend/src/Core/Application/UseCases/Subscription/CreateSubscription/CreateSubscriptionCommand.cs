using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.CreateSubscription;

public record CreateSubscriptionCommand(Guid UserId, int Credits, DateTime? ExpiresAt) : IRequest<SubscriptionEntity>;