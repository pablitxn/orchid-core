using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.GetPortalSession;

public class GetPortalSessionHandler(
    ISubscriptionRepository subscriptionRepository,
    IStripeService stripeService,
    ILogger<GetPortalSessionHandler> logger)
    : IRequestHandler<GetPortalSessionQuery, PortalSessionDto>
{
    public async Task<PortalSessionDto> Handle(GetPortalSessionQuery request, CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        if (string.IsNullOrEmpty(subscription.StripeCustomerId))
            throw new ValidationException("User does not have a Stripe customer account.", nameof(subscription.StripeCustomerId));

        var portalUrl = await stripeService.CreateBillingPortalSessionAsync(
            subscription.StripeCustomerId,
            request.ReturnUrl,
            cancellationToken);

        logger.LogInformation(
            "Created billing portal session for user {UserId}",
            request.UserId);

        return new PortalSessionDto(portalUrl);
    }
}
