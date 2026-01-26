using Application.Interfaces;
using Application.UseCases.Billing.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Billing.GetBillingPaymentMethods;

public sealed class GetBillingPaymentMethodsHandler(
    ISubscriptionRepository subscriptionRepository,
    IStripeService stripeService,
    ILogger<GetBillingPaymentMethodsHandler> logger)
    : IRequestHandler<GetBillingPaymentMethodsQuery, List<PaymentMethodDto>>
{
    public async Task<List<PaymentMethodDto>> Handle(
        GetBillingPaymentMethodsQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription?.StripeCustomerId is null)
        {
            logger.LogInformation("User {UserId} has no Stripe customer ID", request.UserId);
            return new List<PaymentMethodDto>();
        }

        var paymentMethods = await stripeService.GetPaymentMethodsAsync(
            subscription.StripeCustomerId,
            cancellationToken);

        var defaultPaymentMethodId = await stripeService.GetDefaultPaymentMethodIdAsync(
            subscription.StripeCustomerId,
            cancellationToken);

        return paymentMethods.Select(pm => MapToDto(pm, pm.Id == defaultPaymentMethodId)).ToList();
    }

    private static PaymentMethodDto MapToDto(StripePaymentMethodInfo pm, bool isDefault) => new()
    {
        Id = pm.Id,
        Type = pm.Type,
        IsDefault = isDefault,
        CreatedAt = pm.Created,
        Card = pm.Card is null ? null : new CardDetailsDto
        {
            Brand = pm.Card.Brand,
            Last4 = pm.Card.Last4,
            ExpMonth = pm.Card.ExpMonth,
            ExpYear = pm.Card.ExpYear,
            Funding = pm.Card.Funding,
            Country = pm.Card.Country
        }
    };
}
