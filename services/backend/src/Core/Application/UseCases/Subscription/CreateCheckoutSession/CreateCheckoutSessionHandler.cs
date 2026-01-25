using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.CreateCheckoutSession;

public class CreateCheckoutSessionHandler(
    IUserRepository userRepository,
    ISubscriptionRepository subscriptionRepository,
    ISubscriptionPlanRepository planRepository,
    IStripeService stripeService,
    ILogger<CreateCheckoutSessionHandler> logger)
    : IRequestHandler<CreateCheckoutSessionCommand, CheckoutSessionDto>
{
    public async Task<CheckoutSessionDto> Handle(CreateCheckoutSessionCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            throw new UserNotFoundException(request.UserId);

        var plan = await planRepository.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan is null)
            throw new SubscriptionPlanNotFoundException(request.PlanId);

        if (string.IsNullOrEmpty(plan.StripePriceId))
            throw new ValidationException($"Plan '{plan.Name}' is not configured for Stripe payments.", nameof(plan.StripePriceId));

        // Get or create Stripe customer
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        string customerId;

        if (!string.IsNullOrEmpty(subscription?.StripeCustomerId))
        {
            customerId = subscription.StripeCustomerId;
        }
        else
        {
            customerId = await stripeService.GetOrCreateCustomerAsync(
                request.UserId,
                user.Email,
                user.Name,
                cancellationToken);

            // Update subscription with Stripe customer ID if exists
            if (subscription is not null)
            {
                subscription.StripeCustomerId = customerId;
                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }

        // Create checkout session with idempotency key
        var idempotencyKey = $"checkout_{request.UserId}_{request.PlanId}_{DateTime.UtcNow:yyyyMMddHH}";

        var result = await stripeService.CreateCheckoutSessionAsync(
            customerId,
            plan.StripePriceId,
            request.SuccessUrl,
            request.CancelUrl,
            idempotencyKey,
            cancellationToken);

        logger.LogInformation(
            "Created checkout session {SessionId} for user {UserId} subscribing to plan {PlanId}",
            result.SessionId, request.UserId, request.PlanId);

        return new CheckoutSessionDto(result.SessionId, result.Url);
    }
}
