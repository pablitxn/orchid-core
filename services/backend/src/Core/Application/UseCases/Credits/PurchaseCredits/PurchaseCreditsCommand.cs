using Application.UseCases.Credits.Common;
using MediatR;

namespace Application.UseCases.Credits.PurchaseCredits;

public sealed record PurchaseCreditsCommand(
    Guid UserId,
    Guid PackageId,
    string SuccessUrl,
    string CancelUrl) : IRequest<PurchaseCreditsDto>;
