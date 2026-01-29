namespace Application.Interfaces;

/// <summary>
/// Service for recording plugin usage events.
/// All methods are non-blocking - failures are logged but don't break the calling code.
/// </summary>
public interface IPluginUsageTracker
{
    Task TrackInstallAsync(
        Guid pluginId,
        Guid userId,
        string pluginName,
        CancellationToken cancellationToken = default);

    Task TrackUninstallAsync(
        Guid pluginId,
        Guid userId,
        string pluginName,
        CancellationToken cancellationToken = default);

    Task TrackInvocationAsync(
        Guid pluginId,
        Guid userId,
        Guid? agentId,
        string pluginName,
        string functionName,
        long durationMs,
        CancellationToken cancellationToken = default);

    Task TrackErrorAsync(
        Guid pluginId,
        Guid userId,
        Guid? agentId,
        string pluginName,
        string functionName,
        string errorMessage,
        string? errorType,
        CancellationToken cancellationToken = default);

    Task TrackReviewCreatedAsync(
        Guid pluginId,
        Guid reviewId,
        Guid userId,
        int rating,
        CancellationToken cancellationToken = default);
}
