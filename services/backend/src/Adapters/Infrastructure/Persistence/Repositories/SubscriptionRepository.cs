using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SubscriptionRepository(ApplicationDbContext db) : ISubscriptionRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<SubscriptionEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.Subscriptions
            .Include(s => s.SubscriptionPlan)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<SubscriptionEntity> CreateAsync(SubscriptionEntity subscription,
        CancellationToken cancellationToken = default)
    {
        _db.Subscriptions.Add(subscription);
        await _db.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    public async Task UpdateAsync(SubscriptionEntity subscription, CancellationToken cancellationToken = default)
    {
        // Detach navigation property to avoid tracking conflicts
        subscription.SubscriptionPlan = null;

        // Get the current version from DB to handle concurrency token correctly
        var currentVersion = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Id == subscription.Id)
            .Select(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = _db.Entry(subscription);
        if (entry.State == EntityState.Detached)
        {
            _db.Subscriptions.Attach(subscription);
            entry.State = EntityState.Modified;
        }

        // Set the original value for Version to match what's in the database
        entry.Property(s => s.Version).OriginalValue = currentVersion;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithVersionCheckAsync(SubscriptionEntity subscription, int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(subscription);

        // If entity is detached (e.g., from AsNoTracking query), attach and mark as Modified
        if (entry.State == EntityState.Detached)
        {
            _db.Subscriptions.Attach(subscription);
            entry.State = EntityState.Modified;
        }

        // Set the expected original value for the Version property
        entry.Property("Version").OriginalValue = expectedVersion;
        // IsConcurrencyToken is configured in OnModelCreating, not here

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var actualVersion = await _db.Subscriptions
                .AsNoTracking()
                .Where(s => s.Id == subscription.Id)
                .Select(s => s.Version)
                .FirstOrDefaultAsync(cancellationToken);

            throw new ConcurrencyException(
                nameof(SubscriptionEntity),
                subscription.Id,
                expectedVersion,
                actualVersion);
        }
    }
}