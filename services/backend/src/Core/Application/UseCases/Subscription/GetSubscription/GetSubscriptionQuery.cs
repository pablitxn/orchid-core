using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.GetSubscription;

public record GetSubscriptionQuery(Guid UserId) : IRequest<SubscriptionDto?>;