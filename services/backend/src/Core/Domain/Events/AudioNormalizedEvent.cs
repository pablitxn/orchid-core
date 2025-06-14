using Domain.Common;

namespace Domain.Events;

/// <summary>
///     Event that indicates an audio file has been normalized.
/// </summary>
public record AudioNormalizedEvent(
    Guid ProjectId,
    string ProjectName,
    DateTime NormalizedAt,
    string Message
) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}