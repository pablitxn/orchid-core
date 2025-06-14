using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

public sealed class LoginHistoryRepository : ILoginHistoryRepository
{
    private readonly ApplicationDbContext _context;

    public LoginHistoryRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<LoginHistoryEntity> CreateAsync(LoginHistoryEntity loginHistory, CancellationToken cancellationToken = default)
    {
        _context.LoginHistories.Add(loginHistory);
        await _context.SaveChangesAsync(cancellationToken);
        return loginHistory;
    }

    public async Task<List<LoginHistoryEntity>> GetRecentByUserIdAsync(Guid userId, int count, CancellationToken cancellationToken = default)
    {
        return await _context.LoginHistories
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetFailedAttemptsCountAsync(Guid userId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await _context.LoginHistories
            .Where(l => l.UserId == userId && !l.Success && l.Timestamp >= since)
            .CountAsync(cancellationToken);
    }

    public async Task<LoginHistoryEntity?> GetLastSuccessfulLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.LoginHistories
            .Where(l => l.UserId == userId && l.Success)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync(cancellationToken);
    }
}