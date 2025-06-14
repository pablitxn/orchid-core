using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.SubscriptionPlan.GetPlan;

public class GetSubscriptionPlanHandler(ISubscriptionPlanRepository repository)
    : IRequestHandler<GetSubscriptionPlanQuery, SubscriptionPlanEntity?>
{
    private readonly ISubscriptionPlanRepository _repository = repository;

    public async Task<SubscriptionPlanEntity?> Handle(GetSubscriptionPlanQuery request,
        CancellationToken cancellationToken)
    {
        return await _repository.GetByPlanAsync(request.PlanEnum, cancellationToken);
    }
}