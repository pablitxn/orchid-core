using MediatR;

namespace Application.UseCases.Subscription.CreateCheckoutSession;

/// <summary>
/// Command to create a Stripe Checkout session for subscribing to a plan.
/// </summary>
public record CreateCheckoutSessionCommand(
    Guid UserId,
    Guid PlanId,
    string SuccessUrl,
    string CancelUrl) : IRequest<CheckoutSessionDto>;

/// <summary>
/// Result of creating a checkout session.
/// </summary>
public sealed record CheckoutSessionDto(
    string SessionId,
    string Url);
