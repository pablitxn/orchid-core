using System.Text.Json;
using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Caching.Distributed;

namespace WebApi.Hubs;

/// <summary>
///     Publishes activities via SignalR ChatHub.
/// </summary>
public class ActivityPublisher(IHubContext<ChatHub> hubContext, IDistributedCache cache) : IActivityPublisher
{
    private const string HistoryKey = "activity_history";

    public Task PublishAsync(string type, object? payload = null, CancellationToken cancellationToken = default)
    {
        var activity = new Activity(type, payload);

        // 1. Persist activity in Redis history
        _ = PersistAsync(activity, cancellationToken);

        // 2. Broadcast to connected clients
        return hubContext.Clients.All.SendAsync("ReceiveActivity", activity, cancellationToken);
    }

    private async Task PersistAsync(Activity activity, CancellationToken cancellationToken)
    {
        try
        {
            // Retrieve existing history
            var historyJson = await cache.GetStringAsync(HistoryKey, cancellationToken);
            List<Activity> history;
            if (string.IsNullOrEmpty(historyJson))
                history = new List<Activity>();
            else
                history = JsonSerializer.Deserialize<List<Activity>>(historyJson) ?? new List<Activity>();

            history.Add(activity);

            // You may wish to set a max length. For now, keep everything.

            await cache.SetStringAsync(
                HistoryKey,
                JsonSerializer.Serialize(history),
                cancellationToken);
        }
        catch
        {
            // Swallow any cache errors to avoid impacting the main flow.
        }
    }
}