using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Adapters;

/// <summary>
/// SignalR adapter implementation of IRealtimeNotificationPort
/// </summary>
public class SignalRNotificationAdapter : IRealtimeNotificationPort
{
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly ILogger<SignalRNotificationAdapter> _logger;

    public SignalRNotificationAdapter(
        IHubContext<NotificationHub> notificationHub,
        ILogger<SignalRNotificationAdapter> logger)
    {
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task SendNotificationAsync(
        Guid userId, 
        object notificationData, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _notificationHub.Clients
                .Group($"user-{userId}")
                .SendAsync("NewNotification", notificationData, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send real-time notification to user {UserId}", userId);
        }
    }
}