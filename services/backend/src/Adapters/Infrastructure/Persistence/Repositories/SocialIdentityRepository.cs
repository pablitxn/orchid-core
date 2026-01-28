using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for managing user social identities.
/// </summary>
public class SocialIdentityRepository(ApplicationDbContext db) : ISocialIdentityRepository
{
    public async Task<UserSocialIdentityEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await db.UserSocialIdentities
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<UserSocialIdentityEntity?> GetByProviderAsync(
        Guid userId, string provider, CancellationToken ct = default)
    {
        return await db.UserSocialIdentities
            .FirstOrDefaultAsync(i => i.UserId == userId
                && i.Provider == provider
                && i.UnlinkedAt == null, ct);
    }

    public async Task<IReadOnlyList<UserSocialIdentityEntity>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
    {
        return await db.UserSocialIdentities
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.LinkedAt)
            .ToListAsync(ct);
    }

    public async Task<UserSocialIdentityEntity?> GetByProviderUserIdAsync(
        string provider, string providerUserId, CancellationToken ct = default)
    {
        return await db.UserSocialIdentities
            .FirstOrDefaultAsync(i => i.Provider == provider
                && i.ProviderUserId == providerUserId
                && i.UnlinkedAt == null, ct);
    }

    public async Task<UserSocialIdentityEntity> CreateAsync(
        UserSocialIdentityEntity entity, CancellationToken ct = default)
    {
        db.UserSocialIdentities.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(UserSocialIdentityEntity entity, CancellationToken ct = default)
    {
        db.UserSocialIdentities.Update(entity);
        await db.SaveChangesAsync(ct);
    }
}
