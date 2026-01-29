using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Service for recording plugin usage events.
/// All methods are non-blocking - failures are logged but don't break the calling code.
/// </summary>
public class PluginUsageTrackerService : IPluginUsageTracker
{
    private readonly IPluginUsageEventRepository _eventRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PluginUsageTrackerService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public PluginUsageTrackerService(
        IPluginUsageEventRepository eventRepository,
        IEventPublisher eventPublisher,
        ILogger<PluginUsageTrackerService> logger)
    {
        _eventRepository = eventRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task TrackInstallAsync(
        Guid pluginId,
        Guid userId,
        string pluginName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new PluginUsageEventEntity
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                UserId = userId,
                EventType = "installed",
                OccurredAt = DateTime.UtcNow
            };

            await _eventRepository.CreateAsync(entity, cancellationToken);

            // Record Prometheus metrics
            ApplicationMetrics.PluginInstallsTotal.WithLabels(pluginId.ToString(), pluginName).Inc();

            // Publish domain event
            await _eventPublisher.PublishAsync(
                new PluginInstalledEvent(pluginId, userId, pluginName),
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin install: {PluginId} by user {UserId}",
                pluginId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to track plugin install for {PluginId}",
                pluginId);
        }
    }

    public async Task TrackUninstallAsync(
        Guid pluginId,
        Guid userId,
        string pluginName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new PluginUsageEventEntity
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                UserId = userId,
                EventType = "uninstalled",
                OccurredAt = DateTime.UtcNow
            };

            await _eventRepository.CreateAsync(entity, cancellationToken);

            // Record Prometheus metrics
            ApplicationMetrics.PluginUninstallsTotal.WithLabels(pluginId.ToString(), pluginName).Inc();

            // Publish domain event
            await _eventPublisher.PublishAsync(
                new PluginUninstalledEvent(pluginId, userId, pluginName),
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin uninstall: {PluginId} by user {UserId}",
                pluginId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to track plugin uninstall for {PluginId}",
                pluginId);
        }
    }

    public async Task TrackInvocationAsync(
        Guid pluginId,
        Guid userId,
        Guid? agentId,
        string pluginName,
        string functionName,
        long durationMs,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new PluginUsageEventEntity
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                UserId = userId,
                AgentId = agentId,
                EventType = "invoked",
                Metadata = JsonSerializer.Serialize(new { functionName }, JsonOptions),
                DurationMs = durationMs,
                OccurredAt = DateTime.UtcNow
            };

            await _eventRepository.CreateAsync(entity, cancellationToken);

            // Record Prometheus metrics
            ApplicationMetrics.PluginInvocationsTotal.WithLabels(pluginId.ToString(), pluginName, "success").Inc();
            ApplicationMetrics.PluginInvocationDuration.WithLabels(pluginId.ToString(), pluginName).Observe(durationMs / 1000.0);

            // Publish domain event
            await _eventPublisher.PublishAsync(
                new PluginInvokedEvent(pluginId, userId, agentId, pluginName, functionName, durationMs, true),
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin invocation: {PluginId}.{FunctionName} by user {UserId} ({DurationMs}ms)",
                pluginId, functionName, userId, durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to track plugin invocation for {PluginId}",
                pluginId);
        }
    }

    public async Task TrackErrorAsync(
        Guid pluginId,
        Guid userId,
        Guid? agentId,
        string pluginName,
        string functionName,
        string errorMessage,
        string? errorType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new PluginUsageEventEntity
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                UserId = userId,
                AgentId = agentId,
                EventType = "error",
                Metadata = JsonSerializer.Serialize(new { functionName, errorType }, JsonOptions),
                ErrorMessage = errorMessage.Length > 1000 ? errorMessage[..1000] : errorMessage,
                OccurredAt = DateTime.UtcNow
            };

            await _eventRepository.CreateAsync(entity, cancellationToken);

            // Record Prometheus metrics
            ApplicationMetrics.PluginInvocationsTotal.WithLabels(pluginId.ToString(), pluginName, "error").Inc();

            // Publish domain event
            await _eventPublisher.PublishAsync(
                new PluginErrorEvent(pluginId, userId, agentId, pluginName, functionName, errorMessage, errorType),
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin error: {PluginId}.{FunctionName} - {ErrorMessage}",
                pluginId, functionName, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to track plugin error for {PluginId}",
                pluginId);
        }
    }

    public async Task TrackReviewCreatedAsync(
        Guid pluginId,
        Guid reviewId,
        Guid userId,
        int rating,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = new PluginUsageEventEntity
            {
                Id = Guid.NewGuid(),
                PluginId = pluginId,
                UserId = userId,
                EventType = "review_created",
                Metadata = JsonSerializer.Serialize(new { reviewId, rating }, JsonOptions),
                OccurredAt = DateTime.UtcNow
            };

            await _eventRepository.CreateAsync(entity, cancellationToken);

            // Publish domain event
            await _eventPublisher.PublishAsync(
                new PluginReviewCreatedEvent(pluginId, reviewId, userId, rating),
                cancellationToken);

            _logger.LogDebug(
                "Tracked plugin review: {PluginId} by user {UserId} with rating {Rating}",
                pluginId, userId, rating);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to track plugin review for {PluginId}",
                pluginId);
        }
    }
}
