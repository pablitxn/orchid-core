namespace Application.Interfaces;

/// <summary>
/// Port for notifying users about model change events via real-time channels.
/// </summary>
public interface IModelChangeNotificationPort
{
    /// <summary>
    /// Notifies session participants that the model has been changed.
    /// </summary>
    /// <param name="sessionId">The chat session ID.</param>
    /// <param name="previousModel">The model that was previously active.</param>
    /// <param name="newModel">The new active model.</param>
    /// <param name="changedBy">User ID who made the change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyModelChangedAsync(
        string sessionId,
        string? previousModel,
        string newModel,
        Guid changedBy,
        CancellationToken cancellationToken = default);
}
