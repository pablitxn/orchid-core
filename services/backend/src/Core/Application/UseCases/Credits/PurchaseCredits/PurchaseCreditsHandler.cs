using Application.Interfaces;
using Application.UseCases.Credits.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Credits.PurchaseCredits;

public sealed class PurchaseCreditsHandler(
    IUserRepository userRepository,
    ISubscriptionRepository subscriptionRepository,
    ICreditPackageRepository packageRepository,
    ICreditPurchaseRepository purchaseRepository,
    IStripeService stripeService,
    ILogger<PurchaseCreditsHandler> logger)
    : IRequestHandler<PurchaseCreditsCommand, PurchaseCreditsDto>
{
    public async Task<PurchaseCreditsDto> Handle(
        PurchaseCreditsCommand request,
        CancellationToken cancellationToken)
    {
        // Validate user exists
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException(request.UserId);

        // Validate package exists and is active
        var package = await packageRepository.GetByIdAsync(request.PackageId, cancellationToken)
            ?? throw new CreditPackageNotFoundException(request.PackageId);

        if (!package.IsActive)
        {
            throw new ValidationException(
                $"Credit package '{package.Name}' is not available for purchase.",
                nameof(request.PackageId));
        }

        if (string.IsNullOrEmpty(package.StripePriceId))
        {
            throw new ValidationException(
                $"Credit package '{package.Name}' is not configured for Stripe payments.",
                nameof(package.StripePriceId));
        }

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

            // Update subscription with customer ID if exists
            if (subscription is not null)
            {
                subscription.StripeCustomerId = customerId;
                await subscriptionRepository.UpdateAsync(subscription, cancellationToken);
            }
        }

        // Create pending purchase record
        var purchase = new CreditPurchaseEntity
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            CreditPackageId = request.PackageId,
            CreditsGranted = package.Credits,
            PricePaidCents = package.PriceInCents,
            Currency = package.Currency,
            Status = CreditPurchaseStatus.Pending,
            CreditsExpiresAt = package.ExpirationDays.HasValue
                ? DateTime.UtcNow.AddDays(package.ExpirationDays.Value)
                : null
        };

        await purchaseRepository.CreateAsync(purchase, cancellationToken);

        // Create one-time payment checkout session
        var idempotencyKey = $"credit_purchase_{purchase.Id}";

        var result = await stripeService.CreateCreditPurchaseCheckoutSessionAsync(
            customerId,
            package.StripePriceId,
            purchase.Id,
            request.SuccessUrl,
            request.CancelUrl,
            idempotencyKey,
            cancellationToken);

        // Store checkout session ID
        purchase.StripeCheckoutSessionId = result.SessionId;
        await purchaseRepository.UpdateAsync(purchase, cancellationToken);

        logger.LogInformation(
            "Created credit purchase checkout session {SessionId} for user {UserId}, package {PackageId} ({Credits} credits)",
            result.SessionId, request.UserId, request.PackageId, package.Credits);

        return new PurchaseCreditsDto
        {
            SessionId = result.SessionId,
            Url = result.Url
        };
    }
}
