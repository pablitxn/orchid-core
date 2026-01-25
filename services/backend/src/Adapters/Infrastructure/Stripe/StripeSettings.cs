namespace Infrastructure.Stripe;

/// <summary>
/// Configuration settings for Stripe integration.
/// </summary>
public class StripeSettings
{
    public const string SectionName = "Stripe";

    /// <summary>
    /// Stripe secret API key (sk_test_xxx or sk_live_xxx).
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Stripe publishable API key (pk_test_xxx or pk_live_xxx).
    /// </summary>
    public required string PublishableKey { get; init; }

    /// <summary>
    /// Webhook signing secret for verifying webhook signatures.
    /// </summary>
    public required string WebhookSecret { get; init; }

    /// <summary>
    /// Base URL for success/cancel redirects (e.g., "https://app.example.com").
    /// </summary>
    public string? BaseUrl { get; init; }
}
