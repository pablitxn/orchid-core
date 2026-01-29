using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using NCrontab;
using System.Text.Json;

namespace Infrastructure.Services;

/// <summary>
/// Background service that processes scheduled notifications
/// </summary>
public class NotificationSchedulerBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationSchedulerBackgroundService> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly int _batchSize;
    private readonly int _maxRetries;

    public NotificationSchedulerBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<NotificationSchedulerBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var intervalSeconds = configuration.GetValue("NotificationScheduler:CheckIntervalSeconds", 30);
        _checkInterval = TimeSpan.FromSeconds(intervalSeconds);
        _batchSize = configuration.GetValue("NotificationScheduler:BatchSize", 50);
        _maxRetries = configuration.GetValue("NotificationScheduler:MaxRetries", 3);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification scheduler service started with interval {Interval}s", _checkInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingNotificationsAsync(stoppingToken);
                await ProcessRecurringNotificationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification scheduler");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Notification scheduler service stopped");
    }

    private async Task ProcessPendingNotificationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduledRepo = scope.ServiceProvider.GetRequiredService<IScheduledNotificationRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var prefsRepo = scope.ServiceProvider.GetRequiredService<INotificationPreferencesRepository>();

        var pendingNotifications = await scheduledRepo.GetPendingNotificationsAsync(
            DateTime.UtcNow, _batchSize, ct);

        foreach (var scheduled in pendingNotifications)
        {
            try
            {
                var prefs = await prefsRepo.GetOrCreateDefaultAsync(scheduled.UserId, ct);

                if (IsInQuietHours(prefs))
                {
                    var nextNonQuietTime = GetNextNonQuietTime(prefs);
                    scheduled.ScheduledFor = nextNonQuietTime;
                    await scheduledRepo.UpdateAsync(scheduled, ct);
                    _logger.LogDebug("Deferred notification {NotificationId} to {NextTime} due to quiet hours",
                        scheduled.Id, nextNonQuietTime);
                    continue;
                }

                scheduled.Status = ScheduledNotificationStatus.Processing;
                await scheduledRepo.UpdateAsync(scheduled, ct);

                await notificationService.SendNotificationAsync(
                    scheduled.UserId,
                    scheduled.Type,
                    scheduled.Title,
                    scheduled.Message,
                    scheduled.Priority,
                    metadata: scheduled.Metadata,
                    cancellationToken: ct);

                scheduled.Status = ScheduledNotificationStatus.Completed;
                scheduled.LastExecutedAt = DateTime.UtcNow;
                await scheduledRepo.UpdateAsync(scheduled, ct);

                _logger.LogInformation(
                    "Processed scheduled notification {NotificationId} for user {UserId}",
                    scheduled.Id, scheduled.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process scheduled notification {NotificationId}",
                    scheduled.Id);

                scheduled.RetryCount++;
                scheduled.LastError = ex.Message;

                if (scheduled.RetryCount >= _maxRetries)
                {
                    scheduled.Status = ScheduledNotificationStatus.Failed;
                    _logger.LogWarning(
                        "Notification {NotificationId} marked as failed after {RetryCount} retries",
                        scheduled.Id, scheduled.RetryCount);
                }
                else
                {
                    scheduled.Status = ScheduledNotificationStatus.Pending;
                    scheduled.ScheduledFor = DateTime.UtcNow.AddMinutes(Math.Pow(2, scheduled.RetryCount));
                }

                await scheduledRepo.UpdateAsync(scheduled, ct);
            }
        }
    }

    private async Task ProcessRecurringNotificationsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var scheduledRepo = scope.ServiceProvider.GetRequiredService<IScheduledNotificationRepository>();

        var recurringNotifications = await scheduledRepo.GetRecurringNotificationsAsync(ct);

        foreach (var recurring in recurringNotifications)
        {
            if (string.IsNullOrEmpty(recurring.CronExpression)) continue;
            if (recurring.NextExecutionAt.HasValue && recurring.NextExecutionAt.Value > DateTime.UtcNow) continue;

            try
            {
                var schedule = CrontabSchedule.Parse(recurring.CronExpression);
                var nextRun = schedule.GetNextOccurrence(DateTime.UtcNow);

                var oneTimeNotification = new ScheduledNotificationEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = recurring.UserId,
                    Type = recurring.Type,
                    Title = recurring.Title,
                    Message = recurring.Message,
                    Priority = recurring.Priority,
                    Metadata = recurring.Metadata,
                    ScheduledFor = DateTime.UtcNow,
                    IsRecurring = false,
                    SourceService = recurring.SourceService,
                    SourceId = recurring.Id.ToString()
                };

                await scheduledRepo.CreateAsync(oneTimeNotification, ct);

                recurring.NextExecutionAt = schedule.GetNextOccurrence(nextRun);
                recurring.LastExecutedAt = DateTime.UtcNow;
                await scheduledRepo.UpdateAsync(recurring, ct);

                _logger.LogDebug(
                    "Created one-time notification from recurring {RecurringId}, next run at {NextRun}",
                    recurring.Id, recurring.NextExecutionAt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to process recurring notification {NotificationId}",
                    recurring.Id);
            }
        }
    }

    private static bool IsInQuietHours(NotificationPreferencesEntity prefs)
    {
        if (!prefs.QuietHoursStart.HasValue || !prefs.QuietHoursEnd.HasValue)
            return false;

        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(prefs.Timezone ?? "UTC");
            var userTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            var currentTime = TimeOnly.FromDateTime(userTime);

            var start = prefs.QuietHoursStart.Value;
            var end = prefs.QuietHoursEnd.Value;

            if (start > end)
            {
                return currentTime >= start || currentTime < end;
            }
            return currentTime >= start && currentTime < end;
        }
        catch
        {
            return false;
        }
    }

    private static DateTime GetNextNonQuietTime(NotificationPreferencesEntity prefs)
    {
        if (!prefs.QuietHoursEnd.HasValue)
            return DateTime.UtcNow;

        try
        {
            var timezone = TimeZoneInfo.FindSystemTimeZoneById(prefs.Timezone ?? "UTC");
            var userNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timezone);
            var endTime = prefs.QuietHoursEnd.Value;

            var targetDate = userNow.Date;
            if (TimeOnly.FromDateTime(userNow) >= endTime)
            {
                targetDate = targetDate.AddDays(1);
            }

            var targetDateTime = targetDate.Add(endTime.ToTimeSpan()).AddMinutes(5);
            return TimeZoneInfo.ConvertTimeToUtc(targetDateTime, timezone);
        }
        catch
        {
            return DateTime.UtcNow.AddHours(8);
        }
    }
}
