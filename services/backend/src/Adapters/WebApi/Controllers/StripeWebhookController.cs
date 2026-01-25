using Application.UseCases.Subscription.ProcessWebhook;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for handling Stripe webhook events.
/// No authorization - Stripe will call this endpoint directly.
/// </summary>
[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController(IMediator mediator, ILogger<StripeWebhookController> logger) : ControllerBase
{
    private const string StripeSignatureHeader = "Stripe-Signature";

    /// <summary>
    /// Receives and processes Stripe webhook events.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> HandleWebhook()
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        var signature = Request.Headers[StripeSignatureHeader].ToString();
        if (string.IsNullOrEmpty(signature))
        {
            logger.LogWarning("Stripe webhook received without signature header");
            return BadRequest("Missing Stripe signature header.");
        }

        var cmd = new ProcessStripeWebhookCommand(payload, signature);
        var result = await mediator.Send(cmd);

        if (result.Success)
        {
            logger.LogInformation("Stripe webhook {EventId} processed successfully", result.EventId);
            return Ok();
        }

        logger.LogWarning("Stripe webhook processing failed: {Message}", result.Message);

        // Return 200 even on processing failure to prevent Stripe retries for non-retryable errors
        // For signature validation failures, return 400
        if (result.Message == "Invalid signature")
            return BadRequest(result.Message);

        return Ok();
    }
}
