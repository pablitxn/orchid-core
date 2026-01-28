using Domain.Enums;

namespace Application.Interfaces;

/// <summary>
/// Port for notifying users about model degradation events via real-time channels.
/// </summary>
public interface IModelDegradationNotificationPort
{
    /// <summary>
    /// Notifies a user that their AI model was degraded to a fallback model.
    /// </summary>
    /// <param name="userId">The user to notify.</param>
    /// <param name="originalModel">The model that was originally requested.</param>
    /// <param name="currentModel">The model that will be used instead.</param>
    /// <param name="reason">Reason for the degradation.</param>
    /// <param name="sessionId">Optional chat session ID for context.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyModelDegradedAsync(
        Guid userId,
        string originalModel,
        string currentModel,
        ModelDegradationReason reason,
        string? sessionId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Notifies a user that a previously degraded model has recovered.
    /// </summary>
    /// <param name="userId">The user to notify.</param>
    /// <param name="modelId">The model that has recovered.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyModelRecoveredAsync(
        Guid userId,
        string modelId,
        CancellationToken ct = default);
}
