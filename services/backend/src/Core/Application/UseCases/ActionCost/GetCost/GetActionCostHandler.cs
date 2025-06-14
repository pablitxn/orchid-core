using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.ActionCost.GetCost;

public class GetActionCostHandler(IActionCostRepository repository)
    : IRequestHandler<GetActionCostQuery, ActionCostEntity?>
{
    private readonly IActionCostRepository _repository = repository;

    public async Task<ActionCostEntity?> Handle(GetActionCostQuery request, CancellationToken cancellationToken)
    {
        return await _repository.GetByActionAsync(request.ActionType, cancellationToken);
    }
}