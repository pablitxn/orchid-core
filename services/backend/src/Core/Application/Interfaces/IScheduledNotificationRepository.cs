using Domain.Entities;

namespace Application.Interfaces;

public interface IScheduledNotificationRepository
{
    Task<ScheduledNotificationEntity> CreateAsync(ScheduledNotificationEntity notification, CancellationToken cancellationToken = default);

    Task<ScheduledNotificationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<ScheduledNotificationEntity>> GetPendingNotificationsAsync(DateTime before, int batchSize, CancellationToken cancellationToken = default);

    Task<IEnumerable<ScheduledNotificationEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task UpdateAsync(ScheduledNotificationEntity notification, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IEnumerable<ScheduledNotificationEntity>> GetRecurringNotificationsAsync(CancellationToken cancellationToken = default);
}
