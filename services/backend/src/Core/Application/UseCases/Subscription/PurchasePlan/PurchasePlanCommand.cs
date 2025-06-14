using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Subscription.PurchasePlan;

public record PurchasePlanCommand(Guid UserId, SubscriptionPlanEnum PlanEnum) : IRequest<SubscriptionEntity>;