using Application.UseCases.AdminBilling.Common;
using MediatR;

namespace Application.UseCases.AdminBilling.AdjustCredits;

/// <summary>
/// Command to manually adjust a user's credits (add or remove).
/// </summary>
public record AdjustCreditsCommand(
    Guid UserId,
    int Amount,
    string Reason,
    Guid AdminUserId
) : IRequest<AdminSubscriptionDto>;
