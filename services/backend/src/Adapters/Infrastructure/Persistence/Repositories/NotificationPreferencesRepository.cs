using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class NotificationPreferencesRepository : INotificationPreferencesRepository
{
    private readonly ApplicationDbContext _context;

    public NotificationPreferencesRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NotificationPreferencesEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.NotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<NotificationPreferencesEntity> CreateAsync(NotificationPreferencesEntity preferences, CancellationToken cancellationToken = default)
    {
        _context.NotificationPreferences.Add(preferences);
        await _context.SaveChangesAsync(cancellationToken);
        return preferences;
    }

    public async Task<NotificationPreferencesEntity> UpdateAsync(NotificationPreferencesEntity preferences, CancellationToken cancellationToken = default)
    {
        preferences.UpdatedAt = DateTime.UtcNow;
        _context.NotificationPreferences.Update(preferences);
        await _context.SaveChangesAsync(cancellationToken);
        return preferences;
    }

    public async Task<NotificationPreferencesEntity> GetOrCreateDefaultAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var existing = await GetByUserIdAsync(userId, cancellationToken);
        if (existing != null)
            return existing;

        var defaultPrefs = new NotificationPreferencesEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationsEnabled = true,
            Timezone = "UTC",
            EnabledTypes = "[\"reminders\",\"insights\",\"ingestion\",\"sync\"]",
            MatrixNotificationsEnabled = true,
            EmailNotificationsEnabled = false
        };

        return await CreateAsync(defaultPrefs, cancellationToken);
    }
}
