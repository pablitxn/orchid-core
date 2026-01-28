namespace Application.Interfaces;

/// <summary>
/// Circuit breaker for AI model availability.
/// Tracks failures and prevents cascading errors by temporarily blocking calls to failing models.
/// </summary>
public interface IModelCircuitBreaker
{
    /// <summary>
    /// Checks if the circuit is open (blocking calls) for the specified model.
    /// </summary>
    Task<bool> IsCircuitOpenAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Records a successful call to the model.
    /// </summary>
    Task RecordSuccessAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Records a failure for the model.
    /// May trigger circuit to open if threshold is reached.
    /// </summary>
    Task RecordFailureAsync(string modelId, ModelFailureType failureType, CancellationToken ct = default);

    /// <summary>
    /// Gets the current state of the circuit for a model.
    /// </summary>
    Task<ModelCircuitState> GetStateAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Manually resets the circuit breaker for a model.
    /// </summary>
    Task ResetAsync(string modelId, CancellationToken ct = default);
}

/// <summary>
/// Type of failure that occurred when calling a model.
/// </summary>
public enum ModelFailureType
{
    /// <summary>
    /// Request timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Rate limit exceeded from provider.
    /// </summary>
    RateLimit,

    /// <summary>
    /// General API error from provider.
    /// </summary>
    ApiError,

    /// <summary>
    /// Model is deprecated or disabled.
    /// </summary>
    Deprecated,

    /// <summary>
    /// Model is temporarily unavailable.
    /// </summary>
    Unavailable
}

/// <summary>
/// Current state of a model's circuit breaker.
/// </summary>
public record ModelCircuitState(
    string ModelId,
    bool IsOpen,
    int FailureCount,
    DateTime? LastFailure,
    DateTime? CooldownEndsAt);
