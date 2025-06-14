using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class UserCreditLimitRepository : IUserCreditLimitRepository
{
    private readonly ApplicationDbContext _context;

    public UserCreditLimitRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UserCreditLimitEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.UserCreditLimits
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<UserCreditLimitEntity>> GetByUserIdAsync(Guid userId, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.UserCreditLimits.Where(l => l.UserId == userId);
        
        if (activeOnly)
        {
            query = query.Where(l => l.IsActive);
        }
        
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<UserCreditLimitEntity?> GetByUserAndTypeAsync(Guid userId, string limitType, string? resourceType = null, CancellationToken cancellationToken = default)
    {
        return await _context.UserCreditLimits
            .FirstOrDefaultAsync(l => l.UserId == userId && l.LimitType == limitType && l.ResourceType == resourceType, cancellationToken);
    }

    public async Task<UserCreditLimitEntity> CreateAsync(UserCreditLimitEntity limit, CancellationToken cancellationToken = default)
    {
        _context.UserCreditLimits.Add(limit);
        await _context.SaveChangesAsync(cancellationToken);
        return limit;
    }

    public async Task UpdateAsync(UserCreditLimitEntity limit, CancellationToken cancellationToken = default)
    {
        _context.UserCreditLimits.Update(limit);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ConsumeLimitsAsync(Guid userId, int credits, string? resourceType = null, CancellationToken cancellationToken = default)
    {
        var limits = await GetByUserIdAsync(userId, activeOnly: true, cancellationToken);
        
        foreach (var limit in limits)
        {
            // Skip if limit doesn't apply to this resource type
            if (limit.ResourceType != null && limit.ResourceType != resourceType)
                continue;
            
            // Check if limit period has expired and reset if needed
            if (limit.PeriodEndDate <= DateTime.UtcNow)
            {
                limit.PeriodStartDate = DateTime.UtcNow;
                limit.PeriodEndDate = limit.LimitType switch
                {
                    "daily" => limit.PeriodStartDate.AddDays(1),
                    "weekly" => limit.PeriodStartDate.AddDays(7),
                    "monthly" => limit.PeriodStartDate.AddMonths(1),
                    _ => limit.PeriodStartDate.AddDays(1)
                };
                limit.ConsumedCredits = 0;
                limit.UpdatedAt = DateTime.UtcNow;
            }
            
            // Consume credits
            limit.ConsumedCredits += credits;
            limit.UpdatedAt = DateTime.UtcNow;
        }
        
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetExpiredLimitsAsync(CancellationToken cancellationToken = default)
    {
        var expiredLimits = await _context.UserCreditLimits
            .Where(l => l.IsActive && l.PeriodEndDate <= DateTime.UtcNow)
            .ToListAsync(cancellationToken);
        
        foreach (var limit in expiredLimits)
        {
            limit.PeriodStartDate = DateTime.UtcNow;
            limit.PeriodEndDate = limit.LimitType switch
            {
                "daily" => limit.PeriodStartDate.AddDays(1),
                "weekly" => limit.PeriodStartDate.AddDays(7),
                "monthly" => limit.PeriodStartDate.AddMonths(1),
                _ => limit.PeriodStartDate.AddDays(1)
            };
            limit.ConsumedCredits = 0;
            limit.UpdatedAt = DateTime.UtcNow;
        }
        
        if (expiredLimits.Any())
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateWithVersionCheckAsync(UserCreditLimitEntity limit, int expectedVersion, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(limit);
        entry.Property("Version").OriginalValue = expectedVersion;
        
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException("The limit has been modified by another operation.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var limit = await GetByIdAsync(id, cancellationToken);
        if (limit != null)
        {
            _context.UserCreditLimits.Remove(limit);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> CheckLimitsAsync(Guid userId, int requestedCredits, string? resourceType = null, CancellationToken cancellationToken = default)
    {
        var limits = await GetByUserIdAsync(userId, activeOnly: true, cancellationToken);
        
        foreach (var limit in limits)
        {
            // Skip if limit doesn't apply to this resource type
            if (limit.ResourceType != null && limit.ResourceType != resourceType)
                continue;
            
            // Check if limit period has expired and reset if needed
            if (limit.PeriodEndDate <= DateTime.UtcNow)
            {
                limit.PeriodStartDate = DateTime.UtcNow;
                limit.PeriodEndDate = limit.LimitType switch
                {
                    "daily" => limit.PeriodStartDate.AddDays(1),
                    "weekly" => limit.PeriodStartDate.AddDays(7),
                    "monthly" => limit.PeriodStartDate.AddMonths(1),
                    _ => limit.PeriodStartDate.AddDays(1)
                };
                limit.ConsumedCredits = 0;
                limit.UpdatedAt = DateTime.UtcNow;
                await UpdateAsync(limit, cancellationToken);
            }
            
            // Check if adding requested credits would exceed the limit
            if (limit.ConsumedCredits + requestedCredits > limit.MaxCredits)
            {
                return false;
            }
        }
        
        return true;
    }
}