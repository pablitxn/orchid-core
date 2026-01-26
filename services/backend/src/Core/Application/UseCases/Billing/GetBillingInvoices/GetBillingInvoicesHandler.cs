using Application.Interfaces;
using Application.UseCases.Billing.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Billing.GetBillingInvoices;

public sealed class GetBillingInvoicesHandler(
    ISubscriptionRepository subscriptionRepository,
    IStripeService stripeService,
    ILogger<GetBillingInvoicesHandler> logger)
    : IRequestHandler<GetBillingInvoicesQuery, List<InvoiceDto>>
{
    public async Task<List<InvoiceDto>> Handle(
        GetBillingInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription?.StripeCustomerId is null)
        {
            logger.LogInformation("User {UserId} has no Stripe customer ID", request.UserId);
            return new List<InvoiceDto>();
        }

        var invoices = await stripeService.GetInvoicesAsync(
            subscription.StripeCustomerId,
            request.Limit,
            cancellationToken);

        return invoices.Select(MapToDto).ToList();
    }

    private static InvoiceDto MapToDto(StripeInvoiceInfo invoice) => new()
    {
        Id = invoice.Id,
        Number = invoice.Number ?? string.Empty,
        Status = invoice.Status,
        AmountDue = invoice.AmountDue,
        AmountPaid = invoice.AmountPaid,
        Currency = invoice.Currency,
        CreatedAt = invoice.Created,
        PaidAt = invoice.PaidAt,
        DueDate = invoice.DueDate,
        PdfUrl = invoice.PdfUrl,
        HostedInvoiceUrl = invoice.HostedInvoiceUrl,
        Description = invoice.Description,
        LineItems = invoice.LineItems.Select(li => new InvoiceLineItemDto
        {
            Id = li.Id,
            Description = li.Description ?? string.Empty,
            Amount = li.Amount,
            Quantity = li.Quantity,
            PriceId = li.PriceId
        }).ToList()
    };
}
