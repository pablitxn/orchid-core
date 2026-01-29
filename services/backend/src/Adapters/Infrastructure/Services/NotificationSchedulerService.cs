using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using Microsoft.Extensions.Logging;
using NCrontab;

namespace Infrastructure.Services;

/// <summary>
/// Service for scheduling notifications for future delivery
/// </summary>
public class NotificationSchedulerService : INotificationScheduler
{
    private readonly IScheduledNotificationRepository _repository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<NotificationSchedulerService> _logger;

    public NotificationSchedulerService(
        IScheduledNotificationRepository repository,
        IEventPublisher eventPublisher,
        ILogger<NotificationSchedulerService> logger)
    {
        _repository = repository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<Guid> ScheduleAsync(
        Guid userId,
        string type,
        string title,
        string message,
        DateTime scheduledFor,
        int priority = 1,
        string? metadata = null,
        string? sourceService = null,
        string? sourceId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new ScheduledNotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Priority = priority,
            Metadata = metadata,
            ScheduledFor = scheduledFor,
            SourceService = sourceService,
            SourceId = sourceId,
            Status = ScheduledNotificationStatus.Pending
        };

        await _repository.CreateAsync(notification, cancellationToken);

        await _eventPublisher.PublishAsync(
            new NotificationScheduledEvent(
                notification.Id,
                userId,
                type,
                scheduledFor,
                sourceService),
            cancellationToken);

        _logger.LogInformation(
            "Scheduled notification {NotificationId} for user {UserId} at {ScheduledFor}",
            notification.Id, userId, scheduledFor);

        return notification.Id;
    }

    public async Task<Guid> ScheduleRecurringAsync(
        Guid userId,
        string type,
        string title,
        string message,
        string cronExpression,
        int priority = 1,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var schedule = CrontabSchedule.Parse(cronExpression);
        var nextRun = schedule.GetNextOccurrence(DateTime.UtcNow);

        var notification = new ScheduledNotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            Priority = priority,
            Metadata = metadata,
            ScheduledFor = nextRun,
            CronExpression = cronExpression,
            IsRecurring = true,
            NextExecutionAt = nextRun,
            Status = ScheduledNotificationStatus.Pending
        };

        await _repository.CreateAsync(notification, cancellationToken);

        _logger.LogInformation(
            "Scheduled recurring notification {NotificationId} for user {UserId} with cron {Cron}, next run at {NextRun}",
            notification.Id, userId, cronExpression, nextRun);

        return notification.Id;
    }

    public async Task CancelAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _repository.GetByIdAsync(notificationId, cancellationToken);
        if (notification == null)
        {
            _logger.LogWarning("Cannot cancel notification {NotificationId}: not found", notificationId);
            return;
        }

        notification.Status = ScheduledNotificationStatus.Cancelled;
        await _repository.UpdateAsync(notification, cancellationToken);

        _logger.LogInformation("Cancelled notification {NotificationId}", notificationId);
    }
}
