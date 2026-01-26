using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditBalance;

public sealed record GetCreditBalanceQuery(Guid UserId) : IRequest<CreditBalanceDto>;
