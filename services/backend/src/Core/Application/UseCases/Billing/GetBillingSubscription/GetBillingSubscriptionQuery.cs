using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingSubscription;

/// <summary>
/// Query to get the current subscription details for billing management.
/// </summary>
public sealed record GetBillingSubscriptionQuery(Guid UserId) : IRequest<SubscriptionDto?>;
