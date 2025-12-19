using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

public class RefreshTokenRepository(ApplicationDbContext context) : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _context = context;

    public async Task<RefreshTokenEntity> CreateAsync(RefreshTokenEntity token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Add(token);
        await _context.SaveChangesAsync(ct);
        return token;
    }

    public async Task<RefreshTokenEntity?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        return await _context.RefreshTokens
            .Include(r => r.User)
            .ThenInclude(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(r => r.Token == token, ct);
    }

    public async Task UpdateAsync(RefreshTokenEntity token, CancellationToken ct = default)
    {
        _context.RefreshTokens.Update(token);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RevokeAllUserTokensAsync(Guid userId, string? reason = null, string? ipAddress = null, CancellationToken ct = default)
    {
        var activeTokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = ipAddress;
            token.RevokeReason = reason;
        }

        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<RefreshTokenEntity>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);
    }

    public async Task DeleteExpiredTokensAsync(CancellationToken ct = default)
    {
        var expiredTokens = await _context.RefreshTokens
            .Where(r => r.ExpiresAt < DateTime.UtcNow.AddDays(-7)) // Keep expired tokens for 7 days for audit
            .ToListAsync(ct);

        _context.RefreshTokens.RemoveRange(expiredTokens);
        await _context.SaveChangesAsync(ct);
    }
}
