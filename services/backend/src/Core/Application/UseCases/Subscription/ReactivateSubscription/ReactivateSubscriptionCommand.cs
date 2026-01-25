using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.ReactivateSubscription;

/// <summary>
/// Command to reactivate a subscription that was scheduled for cancellation.
/// </summary>
public record ReactivateSubscriptionCommand(Guid UserId) : IRequest<SubscriptionDto>;
