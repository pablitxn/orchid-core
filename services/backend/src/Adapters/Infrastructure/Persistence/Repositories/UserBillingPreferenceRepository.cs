using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserBillingPreferenceRepository : IUserBillingPreferenceRepository
{
    private readonly ApplicationDbContext _context;

    public UserBillingPreferenceRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserBillingPreferenceEntity?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.UserBillingPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<UserBillingPreferenceEntity> CreateAsync(UserBillingPreferenceEntity preference, CancellationToken cancellationToken = default)
    {
        _context.UserBillingPreferences.Add(preference);
        await _context.SaveChangesAsync(cancellationToken);
        return preference;
    }

    public async Task<UserBillingPreferenceEntity> UpdateAsync(UserBillingPreferenceEntity preference, CancellationToken cancellationToken = default)
    {
        preference.UpdatedAt = DateTime.UtcNow;
        _context.UserBillingPreferences.Update(preference);
        await _context.SaveChangesAsync(cancellationToken);
        return preference;
    }
}