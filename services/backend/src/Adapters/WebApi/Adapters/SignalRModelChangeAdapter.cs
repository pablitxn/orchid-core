using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Adapters;

/// <summary>
/// SignalR adapter for model change notifications.
/// Sends real-time notifications to session participants when the model is changed.
/// </summary>
public class SignalRModelChangeAdapter : IModelChangeNotificationPort
{
    private readonly IHubContext<ChatHubV2> _chatHub;
    private readonly ILogger<SignalRModelChangeAdapter> _logger;

    public SignalRModelChangeAdapter(
        IHubContext<ChatHubV2> chatHub,
        ILogger<SignalRModelChangeAdapter> logger)
    {
        _chatHub = chatHub;
        _logger = logger;
    }

    public async Task NotifyModelChangedAsync(
        string sessionId,
        string? previousModel,
        string newModel,
        Guid changedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _chatHub.Clients
                .Group(sessionId)
                .SendAsync("ModelChanged", new
                {
                    sessionId,
                    previousModel,
                    currentModel = newModel,
                    changedBy = changedBy.ToString(),
                    timestamp = DateTime.UtcNow
                }, cancellationToken);

            _logger.LogInformation(
                "Sent model change notification to session {SessionId}: {Previous} -> {New}",
                sessionId, previousModel ?? "(none)", newModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send model change notification to session {SessionId}",
                sessionId);
        }
    }
}
