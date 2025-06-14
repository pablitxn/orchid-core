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
        _db.Subscriptions.Update(subscription);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithVersionCheckAsync(SubscriptionEntity subscription, int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(subscription);
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