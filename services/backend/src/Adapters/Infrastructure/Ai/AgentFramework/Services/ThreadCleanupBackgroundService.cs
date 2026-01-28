using Application.Interfaces;
using Infrastructure.Telemetry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orchid.Infrastructure.Ai.AgentFramework;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Background service that periodically cleans up expired conversation threads.
/// Runs at configurable intervals and removes threads that have been idle beyond the TTL.
/// </summary>
public sealed class ThreadCleanupBackgroundService : BackgroundService
{
    private readonly IAgentThreadManager _threadManager;
    private readonly ILogger<ThreadCleanupBackgroundService> _logger;
    private readonly ThreadManagerSettings _settings;

    public ThreadCleanupBackgroundService(
        IAgentThreadManager threadManager,
        IOptions<ThreadManagerSettings> settings,
        ILogger<ThreadCleanupBackgroundService> logger)
    {
        _threadManager = threadManager ?? throw new ArgumentNullException(nameof(threadManager));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ThreadCleanupBackgroundService started. Cleanup interval: {IntervalSeconds}s, TTL: {TtlMinutes}min, PersistToRedis: {Persist}",
            _settings.CleanupIntervalSeconds,
            _settings.ThreadIdleTimeoutMinutes,
            _settings.PersistToRedisOnCleanup);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredThreadsAsync(stoppingToken);
                UpdateActiveThreadsMetric();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown, exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during thread cleanup cycle");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_settings.CleanupIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("ThreadCleanupBackgroundService stopped");
    }

    private async Task CleanupExpiredThreadsAsync(CancellationToken cancellationToken)
    {
        var idleTimeout = TimeSpan.FromMinutes(_settings.ThreadIdleTimeoutMinutes);
        var expiredSessionIds = _threadManager.GetExpiredSessionIds(idleTimeout);

        if (expiredSessionIds.Count == 0)
        {
            _logger.LogDebug("No expired threads found during cleanup cycle");
            return;
        }

        _logger.LogInformation(
            "Found {Count} expired threads to cleanup (idle > {TtlMinutes}min)",
            expiredSessionIds.Count,
            _settings.ThreadIdleTimeoutMinutes);

        var cleanedCount = 0;
        foreach (var sessionId in expiredSessionIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await _threadManager.CleanupThreadAsync(
                    sessionId,
                    _settings.PersistToRedisOnCleanup,
                    cancellationToken);

                cleanedCount++;
                ApplicationMetrics.ThreadsCleanedTotal.WithLabels("expired").Inc();

                _logger.LogDebug("Cleaned up expired thread for session {SessionId}", sessionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to cleanup thread for session {SessionId}",
                    sessionId);
            }
        }

        _logger.LogInformation(
            "Cleanup cycle completed. Cleaned {CleanedCount}/{TotalCount} expired threads",
            cleanedCount,
            expiredSessionIds.Count);
    }

    private void UpdateActiveThreadsMetric()
    {
        var activeCount = _threadManager.GetActiveThreadCount();
        ApplicationMetrics.ActiveThreadsGauge.Set(activeCount);
    }
}
