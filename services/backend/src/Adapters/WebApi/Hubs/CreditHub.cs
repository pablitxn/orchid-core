using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time credit updates
/// </summary>
[Authorize]
public class CreditHub : Hub
{
    private readonly ILogger<CreditHub> _logger;

    public CreditHub(ILogger<CreditHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            // Add user to their personal group for targeted updates
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("User {UserId} connected to CreditHub", userId);
        }
        
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user-{userId}");
            _logger.LogInformation("User {UserId} disconnected from CreditHub", userId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to credit updates for a specific user
    /// </summary>
    public async Task SubscribeToCreditUpdates()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("Subscribed", new { userId, timestamp = DateTime.UtcNow });
        }
    }
}