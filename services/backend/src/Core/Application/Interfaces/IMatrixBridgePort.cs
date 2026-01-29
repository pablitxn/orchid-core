namespace Application.Interfaces;

/// <summary>
/// Port for sending notifications via Matrix bridge (matrix-orchid-bot)
/// </summary>
public interface IMatrixBridgePort
{
    /// <summary>
    /// Send notification to Matrix user via bridge service
    /// </summary>
    Task<bool> SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if user has Matrix bridge linked
    /// </summary>
    Task<bool> IsUserLinkedAsync(Guid userId, CancellationToken cancellationToken = default);
}
