using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a model's circuit breaker opens due to repeated failures.
/// </summary>
public record ModelCircuitOpenedEvent(
    string ModelId,
    int FailureCount,
    TimeSpan CooldownPeriod) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
