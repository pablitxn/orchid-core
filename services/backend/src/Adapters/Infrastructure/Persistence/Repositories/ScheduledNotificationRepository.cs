using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class ScheduledNotificationRepository : IScheduledNotificationRepository
{
    private readonly ApplicationDbContext _context;

    public ScheduledNotificationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ScheduledNotificationEntity> CreateAsync(ScheduledNotificationEntity notification, CancellationToken cancellationToken = default)
    {
        _context.ScheduledNotifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);
        return notification;
    }

    public async Task<ScheduledNotificationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledNotifications
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<ScheduledNotificationEntity>> GetPendingNotificationsAsync(
        DateTime before,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledNotifications
            .Where(n => n.Status == ScheduledNotificationStatus.Pending && n.ScheduledFor <= before)
            .OrderBy(n => n.ScheduledFor)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ScheduledNotificationEntity>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledNotifications
            .Where(n => n.UserId == userId && n.Status == ScheduledNotificationStatus.Pending)
            .OrderBy(n => n.ScheduledFor)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(ScheduledNotificationEntity notification, CancellationToken cancellationToken = default)
    {
        notification.UpdatedAt = DateTime.UtcNow;
        _context.ScheduledNotifications.Update(notification);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var notification = await GetByIdAsync(id, cancellationToken);
        if (notification != null)
        {
            _context.ScheduledNotifications.Remove(notification);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<ScheduledNotificationEntity>> GetRecurringNotificationsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledNotifications
            .Where(n => n.IsRecurring && n.Status != ScheduledNotificationStatus.Cancelled)
            .ToListAsync(cancellationToken);
    }
}
