namespace Domain.Entities;

/// <summary>
/// Represents a purchasable credit package with pricing information.
/// </summary>
public class CreditPackageEntity
{
    public Guid Id { get; init; }

    /// <summary>
    /// Display name of the package (e.g., "Starter Pack", "Pro Bundle").
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Number of credits included in this package.
    /// </summary>
    public required int Credits { get; set; }

    /// <summary>
    /// Price in cents (e.g., 999 = $9.99).
    /// </summary>
    public required int PriceInCents { get; set; }

    /// <summary>
    /// Currency code (default: "usd").
    /// </summary>
    public string Currency { get; set; } = "usd";

    /// <summary>
    /// Stripe Price ID for checkout integration.
    /// </summary>
    public string? StripePriceId { get; set; }

    /// <summary>
    /// Whether this package is available for purchase.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Number of days after purchase before credits expire.
    /// Null means credits never expire.
    /// </summary>
    public int? ExpirationDays { get; set; }

    /// <summary>
    /// Display order for UI sorting (lower = first).
    /// </summary>
    public int DisplayOrder { get; set; } = 0;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
