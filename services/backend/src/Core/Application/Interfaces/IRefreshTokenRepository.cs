using Domain.Entities;

namespace Application.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshTokenEntity> CreateAsync(RefreshTokenEntity token, CancellationToken ct = default);
    Task<RefreshTokenEntity?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task UpdateAsync(RefreshTokenEntity token, CancellationToken ct = default);
    Task RevokeAllUserTokensAsync(Guid userId, string? reason = null, string? ipAddress = null, CancellationToken ct = default);
    Task<List<RefreshTokenEntity>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task DeleteExpiredTokensAsync(CancellationToken ct = default);
}
