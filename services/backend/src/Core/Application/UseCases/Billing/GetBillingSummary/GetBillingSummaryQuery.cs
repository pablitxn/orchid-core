using Application.UseCases.Billing.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingSummary;

/// <summary>
/// Query to get a comprehensive billing summary for the authenticated user.
/// </summary>
public sealed record GetBillingSummaryQuery(
    Guid UserId,
    string? ReturnUrl = null) : IRequest<BillingSummaryDto>;
