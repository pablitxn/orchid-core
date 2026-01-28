using Domain.Common;
using Domain.Enums;

namespace Domain.Events;

/// <summary>
/// Domain event raised when an AI model is degraded to a fallback model.
/// </summary>
public record ModelDegradedEvent(
    Guid UserId,
    Guid? AgentId,
    string OriginalModel,
    string FallbackModel,
    ModelDegradationReason Reason,
    string? SessionId = null) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
