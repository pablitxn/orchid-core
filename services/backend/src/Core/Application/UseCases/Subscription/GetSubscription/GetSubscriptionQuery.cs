using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.GetSubscription;

public record GetSubscriptionQuery(Guid UserId) : IRequest<SubscriptionEntity?>;