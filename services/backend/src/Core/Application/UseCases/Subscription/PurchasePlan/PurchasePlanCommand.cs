using Application.UseCases.Subscription.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Subscription.PurchasePlan;

public record PurchasePlanCommand(Guid UserId, SubscriptionPlanEnum PlanEnum) : IRequest<SubscriptionDto>;