namespace Orchid.Infrastructure.Ai.AgentFramework;

/// <summary>
/// Configuration settings for the model circuit breaker.
/// </summary>
public sealed class ModelCircuitBreakerSettings
{
    public const string SectionName = "ModelCircuitBreaker";

    /// <summary>
    /// Whether the circuit breaker is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Number of failures within the sliding window before the circuit opens.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Cooldown period in seconds before the circuit moves to half-open state.
    /// </summary>
    public int CooldownPeriodSeconds { get; set; } = 60;

    /// <summary>
    /// Number of successful calls required in half-open state to close the circuit.
    /// </summary>
    public int SuccessThresholdInHalfOpen { get; set; } = 2;

    /// <summary>
    /// Sliding window duration in seconds for counting failures.
    /// Failures older than this are not counted.
    /// </summary>
    public int SlidingWindowSeconds { get; set; } = 120;
}
