using Application.Interfaces;
using Application.UseCases.Billing.Common;
using Application.UseCases.Subscription.Common;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Billing.GetBillingSummary;

public sealed class GetBillingSummaryHandler(
    ISubscriptionRepository subscriptionRepository,
    IUserCreditsRepository userCreditsRepository,
    ICreditConsumptionRepository creditConsumptionRepository,
    IStripeService stripeService,
    ILogger<GetBillingSummaryHandler> logger)
    : IRequestHandler<GetBillingSummaryQuery, BillingSummaryDto>
{
    public async Task<BillingSummaryDto> Handle(
        GetBillingSummaryQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var userCredits = await userCreditsRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        SubscriptionDto? subscriptionDto = null;
        if (subscription is not null)
        {
            var creditsForDto = userCredits ?? new Domain.Entities.UserCreditsEntity
            {
                Id = Guid.Empty,
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0
            };
            subscriptionDto = SubscriptionDto.FromEntityWithCredits(subscription, creditsForDto);
        }

        var creditBalance = userCredits?.AvailableCredits ?? 0;
        var isUnlimited = subscription?.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;

        // Calculate credits consumed this month
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var consumptions = await creditConsumptionRepository.GetByDateRangeAsync(
            request.UserId, monthStart, now, cancellationToken);
        var creditsConsumedThisMonth = consumptions.Sum(c => c.CreditsConsumed);

        int paymentMethodCount = 0;
        InvoiceDto? latestInvoice = null;
        long currentPeriodSpend = 0;
        string? billingPortalUrl = null;

        if (subscription?.StripeCustomerId is not null)
        {
            var paymentMethods = await stripeService.GetPaymentMethodsAsync(
                subscription.StripeCustomerId,
                cancellationToken);
            paymentMethodCount = paymentMethods.Count;

            var invoices = await stripeService.GetInvoicesAsync(
                subscription.StripeCustomerId,
                limit: 1,
                cancellationToken);

            if (invoices.Count > 0)
            {
                var invoice = invoices[0];
                latestInvoice = new InvoiceDto
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
                currentPeriodSpend = invoice.AmountPaid;
            }

            if (!string.IsNullOrEmpty(request.ReturnUrl))
            {
                try
                {
                    billingPortalUrl = await stripeService.CreateBillingPortalSessionAsync(
                        subscription.StripeCustomerId,
                        request.ReturnUrl,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create billing portal session for user {UserId}", request.UserId);
                }
            }
        }

        return new BillingSummaryDto
        {
            Subscription = subscriptionDto,
            CreditBalance = creditBalance,
            IsUnlimited = isUnlimited,
            PaymentMethodCount = paymentMethodCount,
            LatestInvoice = latestInvoice,
            CurrentPeriodSpend = currentPeriodSpend,
            CreditsConsumedThisMonth = creditsConsumedThisMonth,
            BillingPortalUrl = billingPortalUrl
        };
    }
}
