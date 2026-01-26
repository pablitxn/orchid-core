using Application.UseCases.Billing.Common;
using MediatR;

namespace Application.UseCases.Billing.GetBillingInvoices;

/// <summary>
/// Query to get invoices for the authenticated user.
/// </summary>
public sealed record GetBillingInvoicesQuery(
    Guid UserId,
    int Limit = 10) : IRequest<List<InvoiceDto>>;
