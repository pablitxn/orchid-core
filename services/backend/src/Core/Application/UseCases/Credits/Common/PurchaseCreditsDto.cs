namespace Application.UseCases.Credits.Common;

/// <summary>
/// Result of initiating a credit package purchase (Stripe checkout session).
/// </summary>
public sealed record PurchaseCreditsDto
{
    /// <summary>
    /// Stripe Checkout Session ID.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// URL to redirect the user to complete payment.
    /// </summary>
    public string Url { get; init; } = string.Empty;
}
