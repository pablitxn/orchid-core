namespace Domain.Enums;

/// <summary>
/// Indicates which tier in the resolution hierarchy provided the model.
/// Used for debugging, telemetry, and understanding model selection behavior.
/// </summary>
public enum ModelResolutionSource
{
    /// <summary>
    /// Model was provided as an override in the request.
    /// </summary>
    RequestOverride,

    /// <summary>
    /// Model was configured directly on the agent.
    /// </summary>
    AgentConfiguration,

    /// <summary>
    /// Model was selected based on agent's ModelPreference.
    /// </summary>
    AgentPreference,

    /// <summary>
    /// Model was selected based on user's default preference.
    /// </summary>
    UserPreference,

    /// <summary>
    /// Model was taken from system default configuration.
    /// </summary>
    SystemDefault,

    /// <summary>
    /// Model was taken from emergency fallback constant.
    /// </summary>
    EmergencyFallback,

    /// <summary>
    /// Model was selected due to credit constraints (user cannot afford preferred model).
    /// </summary>
    CreditConstraint,

    /// <summary>
    /// Model was selected because the circuit breaker is open for the preferred model.
    /// </summary>
    CircuitBreakerFallback
}
