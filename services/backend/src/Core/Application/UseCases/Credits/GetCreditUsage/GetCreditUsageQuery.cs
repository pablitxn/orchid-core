using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.GetCreditUsage;

public sealed record GetCreditUsageQuery(
    Guid UserId,
    string Period = "month") : IRequest<CreditUsageDto>;
