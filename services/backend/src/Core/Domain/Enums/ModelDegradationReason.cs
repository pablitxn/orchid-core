namespace Domain.Enums;

/// <summary>
/// Reason why a model degradation occurred during AI completion.
/// </summary>
public enum ModelDegradationReason
{
    /// <summary>
    /// User doesn't have enough credits for the preferred model.
    /// </summary>
    InsufficientCredits,

    /// <summary>
    /// The model is not available (deprecated, disabled, etc.).
    /// </summary>
    ModelUnavailable,

    /// <summary>
    /// The model provider returned a rate limit error.
    /// </summary>
    RateLimited,

    /// <summary>
    /// The model request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// The circuit breaker is open due to repeated failures.
    /// </summary>
    CircuitOpen
}
