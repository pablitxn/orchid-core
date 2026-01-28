using Domain.Common;

namespace Domain.Events;

/// <summary>
/// Domain event raised when a model's circuit breaker closes after recovery.
/// </summary>
public record ModelCircuitClosedEvent(
    string ModelId,
    DateTime RecoveredAt) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
