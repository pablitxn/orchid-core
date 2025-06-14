using Domain.Entities;

namespace Application.Interfaces;

public interface INotificationRepository
{
    Task<NotificationEntity> CreateAsync(NotificationEntity notification, CancellationToken cancellationToken = default);
    
    Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<NotificationEntity>> GetByUserIdAsync(Guid userId, bool includeRead = false, CancellationToken cancellationToken = default);
    
    Task<IEnumerable<NotificationEntity>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task UpdateAsync(NotificationEntity notification, CancellationToken cancellationToken = default);
    
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    
    Task DeleteExpiredAsync(CancellationToken cancellationToken = default);
}