namespace Application.Interfaces;

/// <summary>
/// Port for sending real-time notifications to users
/// </summary>
public interface IRealtimeNotificationPort
{
    /// <summary>
    /// Send a real-time notification to a specific user
    /// </summary>
    Task SendNotificationAsync(
        Guid userId, 
        object notificationData, 
        CancellationToken cancellationToken = default);
}