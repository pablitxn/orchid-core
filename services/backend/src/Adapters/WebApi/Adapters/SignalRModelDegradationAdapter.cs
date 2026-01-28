using Application.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Adapters;

/// <summary>
/// SignalR adapter for model degradation notifications.
/// Sends real-time notifications to users when their AI model is degraded to a fallback.
/// </summary>
public class SignalRModelDegradationAdapter : IModelDegradationNotificationPort
{
    private readonly IHubContext<NotificationHub> _notificationHub;
    private readonly ILogger<SignalRModelDegradationAdapter> _logger;

    public SignalRModelDegradationAdapter(
        IHubContext<NotificationHub> notificationHub,
        ILogger<SignalRModelDegradationAdapter> logger)
    {
        _notificationHub = notificationHub;
        _logger = logger;
    }

    public async Task NotifyModelDegradedAsync(
        Guid userId,
        string originalModel,
        string currentModel,
        ModelDegradationReason reason,
        string? sessionId = null,
        CancellationToken ct = default)
    {
        try
        {
            await _notificationHub.Clients
                .Group($"user-{userId}")
                .SendAsync("ModelDegraded", new
                {
                    originalModel,
                    currentModel,
                    reason = reason.ToString(),
                    sessionId,
                    message = GetDegradationMessage(reason),
                    timestamp = DateTime.UtcNow
                }, ct);

            _logger.LogInformation(
                "Sent model degradation notification to user {UserId}: {Original} -> {Current} ({Reason})",
                userId, originalModel, currentModel, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send model degradation notification to user {UserId}",
                userId);
        }
    }

    public async Task NotifyModelRecoveredAsync(
        Guid userId,
        string modelId,
        CancellationToken ct = default)
    {
        try
        {
            await _notificationHub.Clients
                .Group($"user-{userId}")
                .SendAsync("ModelRecovered", new
                {
                    modelId,
                    message = $"Model {modelId} is now available again.",
                    timestamp = DateTime.UtcNow
                }, ct);

            _logger.LogInformation(
                "Sent model recovery notification to user {UserId}: {ModelId}",
                userId, modelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send model recovery notification to user {UserId}",
                userId);
        }
    }

    private static string GetDegradationMessage(ModelDegradationReason reason) => reason switch
    {
        ModelDegradationReason.InsufficientCredits =>
            "Using a more cost-efficient model due to credit balance.",
        ModelDegradationReason.RateLimited =>
            "Temporarily using an alternative model due to high demand.",
        ModelDegradationReason.Timeout =>
            "Switched to a faster model for better responsiveness.",
        ModelDegradationReason.CircuitOpen =>
            "Using an alternative model while primary recovers.",
        ModelDegradationReason.ModelUnavailable =>
            "Using an alternative model as the preferred model is temporarily unavailable.",
        _ => "Using an alternative AI model."
    };
}
