using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.ChangePlan;

/// <summary>
/// Command to change the subscription to a different plan.
/// </summary>
public record ChangePlanCommand(
    Guid UserId,
    Guid NewPlanId) : IRequest<SubscriptionDto>;
