namespace Application.UseCases.Billing.Common;

/// <summary>
/// Represents a saved payment method.
/// </summary>
public sealed record PaymentMethodDto
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
    public DateTime CreatedAt { get; init; }
    public CardDetailsDto? Card { get; init; }
}

/// <summary>
/// Card-specific payment method details.
/// </summary>
public sealed record CardDetailsDto
{
    public string Brand { get; init; } = string.Empty;
    public string Last4 { get; init; } = string.Empty;
    public int ExpMonth { get; init; }
    public int ExpYear { get; init; }
    public string? Funding { get; init; }
    public string? Country { get; init; }
}
