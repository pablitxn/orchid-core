using Domain.Entities;
using MediatR;

namespace Application.UseCases.ActionCost.GetCost;

public abstract record GetActionCostQuery(string ActionType) : IRequest<ActionCostEntity?>;