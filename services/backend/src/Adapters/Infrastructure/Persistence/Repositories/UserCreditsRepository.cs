using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserCreditsRepository(ApplicationDbContext db) : IUserCreditsRepository
{
    private readonly ApplicationDbContext _db = db;

    public async Task<UserCreditsEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _db.UserCredits
            .AsNoTracking()
            .FirstOrDefaultAsync(uc => uc.UserId == userId, cancellationToken);
    }

    public async Task<UserCreditsEntity> CreateAsync(UserCreditsEntity userCredits, CancellationToken cancellationToken = default)
    {
        _db.UserCredits.Add(userCredits);
        await _db.SaveChangesAsync(cancellationToken);
        return userCredits;
    }

    public async Task UpdateAsync(UserCreditsEntity userCredits, CancellationToken cancellationToken = default)
    {
        var currentVersion = await _db.UserCredits
            .AsNoTracking()
            .Where(uc => uc.Id == userCredits.Id)
            .Select(uc => uc.Version)
            .FirstOrDefaultAsync(cancellationToken);

        var entry = _db.Entry(userCredits);
        if (entry.State == EntityState.Detached)
        {
            _db.UserCredits.Attach(userCredits);
            entry.State = EntityState.Modified;
        }

        entry.Property(uc => uc.Version).OriginalValue = currentVersion;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWithVersionCheckAsync(UserCreditsEntity userCredits, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(userCredits);

        if (entry.State == EntityState.Detached)
        {
            _db.UserCredits.Attach(userCredits);
            entry.State = EntityState.Modified;
        }

        entry.Property("Version").OriginalValue = expectedVersion;

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var actualVersion = await _db.UserCredits
                .AsNoTracking()
                .Where(uc => uc.Id == userCredits.Id)
                .Select(uc => uc.Version)
                .FirstOrDefaultAsync(cancellationToken);

            throw new ConcurrencyException(
                nameof(UserCreditsEntity),
                userCredits.Id,
                expectedVersion,
                actualVersion);
        }
    }

    public async Task<UserCreditsEntity> GetOrCreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userCredits = await _db.UserCredits
            .FirstOrDefaultAsync(uc => uc.UserId == userId, cancellationToken);

        if (userCredits != null)
            return userCredits;

        userCredits = new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = 0,
            TotalCreditsGranted = 0,
            TotalCreditsConsumed = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.UserCredits.Add(userCredits);
        await _db.SaveChangesAsync(cancellationToken);

        return userCredits;
    }
}
