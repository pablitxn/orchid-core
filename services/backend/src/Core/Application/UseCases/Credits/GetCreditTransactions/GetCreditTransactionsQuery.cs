using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditTransactions;

public sealed record GetCreditTransactionsQuery(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    DateTime? StartDate = null,
    DateTime? EndDate = null) : IRequest<CreditTransactionsDto>;
