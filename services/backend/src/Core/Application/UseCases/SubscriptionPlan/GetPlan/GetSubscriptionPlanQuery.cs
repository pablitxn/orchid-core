using Domain.Entities;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.SubscriptionPlan.GetPlan;

public record GetSubscriptionPlanQuery(SubscriptionPlanEnum PlanEnum) : IRequest<SubscriptionPlanEntity?>;