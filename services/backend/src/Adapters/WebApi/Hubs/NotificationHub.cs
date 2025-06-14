using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time notifications
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal group for targeted notifications
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("User {UserId} connected to NotificationHub", userId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("User {UserId} disconnected from NotificationHub", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}