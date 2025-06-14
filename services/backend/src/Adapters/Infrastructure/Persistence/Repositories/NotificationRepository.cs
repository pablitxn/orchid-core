using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<NotificationEntity>> GetByUserIdAsync(Guid userId, bool includeRead = false, CancellationToken cancellationToken = default)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow));
        
        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }
        
        return await query
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationEntity> CreateAsync(NotificationEntity notification, CancellationToken cancellationToken = default)
    {
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task UpdateAsync(NotificationEntity notification, CancellationToken cancellationToken = default)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await GetByIdAsync(id, cancellationToken);
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await UpdateAsync(notification, cancellationToken);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);
        
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }
        
        if (unreadNotifications.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<NotificationEntity>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow))
            .OrderByDescending(n => n.Priority)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead && (n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow), cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await GetByIdAsync(id, cancellationToken);
        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _context.Notifications
            .Where(n => n.ExpiresAt != null && n.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        
        if (expired.Any())
        {
            _context.Notifications.RemoveRange(expired);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}