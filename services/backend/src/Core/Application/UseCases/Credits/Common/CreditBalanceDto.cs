namespace Application.UseCases.Credits.Common;

public sealed record CreditBalanceDto
{
    public int Balance { get; init; }
    public bool IsUnlimited { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool HasSubscription { get; init; }

    /// <summary>
    /// Free tier credits remaining for today (only applicable for free tier users).
    /// </summary>
    public int? FreeTierCreditsRemaining { get; init; }
}
