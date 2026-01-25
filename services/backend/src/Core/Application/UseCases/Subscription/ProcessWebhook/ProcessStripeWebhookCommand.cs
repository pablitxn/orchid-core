using MediatR;

namespace Application.UseCases.Subscription.ProcessWebhook;

/// <summary>
/// Command to process a Stripe webhook event.
/// </summary>
public record ProcessStripeWebhookCommand(
    string Payload,
    string Signature) : IRequest<WebhookProcessingResult>;

/// <summary>
/// Result of webhook processing.
/// </summary>
public sealed record WebhookProcessingResult(
    bool Success,
    string? EventId = null,
    string? Message = null);
