using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationPreferencesRepository
{
    Task<NotificationPreferencesEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesEntity> CreateAsync(NotificationPreferencesEntity preferences, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesEntity> UpdateAsync(NotificationPreferencesEntity preferences, CancellationToken cancellationToken = default);

    Task<NotificationPreferencesEntity> GetOrCreateDefaultAsync(Guid userId, CancellationToken cancellationToken = default);
}
