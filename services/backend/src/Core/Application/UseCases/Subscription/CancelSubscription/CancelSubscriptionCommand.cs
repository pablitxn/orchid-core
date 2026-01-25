using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.CancelSubscription;

/// <summary>
/// Command to cancel a subscription at the end of the current billing period.
/// </summary>
public record CancelSubscriptionCommand(Guid UserId) : IRequest<SubscriptionDto>;
