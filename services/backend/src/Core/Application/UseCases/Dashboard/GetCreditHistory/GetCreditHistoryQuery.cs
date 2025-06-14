using MediatR;
using Core.Application.UseCases.Dashboard.Common;

namespace Core.Application.UseCases.Dashboard.GetCreditHistory;

public sealed record GetCreditHistoryQuery(
    Guid UserId,
    int PageNumber = 1,
    int PageSize = 20,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    string? ConsumptionType = null
) : IRequest<CreditHistoryDto>;