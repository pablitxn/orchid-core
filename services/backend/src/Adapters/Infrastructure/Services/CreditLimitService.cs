using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CreditLimitService : ICreditLimitService
{
    private readonly IUserCreditLimitRepository _limitRepository;
    private readonly ILogger<CreditLimitService> _logger;

    public CreditLimitService(
        IUserCreditLimitRepository limitRepository,
        ILogger<CreditLimitService> logger)
    {
        _limitRepository = limitRepository;
        _logger = logger;
    }

    public async Task<CreditLimitCheckResult> CheckLimitsAsync(
        Guid userId, 
        int requestedCredits, 
        string? resourceType = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var limits = await _limitRepository.GetByUserIdAsync(userId, activeOnly: true, cancellationToken);
            var violations = new List<LimitViolation>();

            foreach (var limit in limits)
            {
                // Skip if limit doesn't apply to this resource type
                if (limit.ResourceType != null && limit.ResourceType != resourceType)
                    continue;

                if (!limit.IsWithinLimit(requestedCredits))
                {
                    violations.Add(new LimitViolation(
                        limit.LimitType,
                        limit.MaxCredits,
                        limit.ConsumedCredits,
                        limit.GetRemainingCredits(),
                        limit.PeriodEndDate,
                        limit.ResourceType
                    ));
                }
            }

            if (violations.Any())
            {
                _logger.LogWarning("Credit limit violations for user {UserId}: {Violations}", 
                    userId, string.Join(", ", violations.Select(v => $"{v.LimitType} limit exceeded")));
            }

            return new CreditLimitCheckResult(violations.Count == 0, violations);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking credit limits for user {UserId}", userId);
            // In case of error, allow the operation (fail open)
            return new CreditLimitCheckResult(true);
        }
    }

    public async Task<bool> ConsumeLimitsAsync(
        Guid userId, 
        int credits, 
        string? resourceType = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _limitRepository.ConsumeLimitsAsync(userId, credits, resourceType, cancellationToken);
            
            _logger.LogInformation("Consumed {Credits} credits against limits for user {UserId}, resource type {ResourceType}", 
                credits, userId, resourceType ?? "all");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error consuming credit limits for user {UserId}", userId);
            return false;
        }
    }

    public async Task<UserCreditLimitsDto> GetUserLimitsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default)
    {
        var limits = await _limitRepository.GetByUserIdAsync(userId, activeOnly: false, cancellationToken);
        
        var limitDtos = limits.Select(l => new UserCreditLimitDto(
            l.Id,
            l.LimitType,
            l.MaxCredits,
            l.ConsumedCredits,
            l.GetRemainingCredits(),
            l.PeriodStartDate,
            l.PeriodEndDate,
            l.IsActive,
            l.ResourceType
        )).ToList();

        return new UserCreditLimitsDto(userId, limitDtos);
    }

    public async Task<UserCreditLimitDto> SetUserLimitAsync(
        Guid userId, 
        string limitType, 
        int maxCredits, 
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        // Check if limit already exists
        var existingLimit = await _limitRepository.GetByUserAndTypeAsync(userId, limitType, resourceType, cancellationToken);
        
        if (existingLimit != null)
        {
            // Update existing limit
            existingLimit.MaxCredits = maxCredits;
            existingLimit.IsActive = true;
            existingLimit.UpdatedAt = DateTime.UtcNow;
            
            await _limitRepository.UpdateAsync(existingLimit, cancellationToken);
            
            _logger.LogInformation("Updated {LimitType} limit for user {UserId} to {MaxCredits} credits", 
                limitType, userId, maxCredits);
            
            return new UserCreditLimitDto(
                existingLimit.Id,
                existingLimit.LimitType,
                existingLimit.MaxCredits,
                existingLimit.ConsumedCredits,
                existingLimit.GetRemainingCredits(),
                existingLimit.PeriodStartDate,
                existingLimit.PeriodEndDate,
                existingLimit.IsActive,
                existingLimit.ResourceType
            );
        }
        else
        {
            // Create new limit
            var newLimit = new UserCreditLimitEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LimitType = limitType,
                MaxCredits = maxCredits,
                ResourceType = resourceType,
                PeriodStartDate = DateTime.UtcNow,
                PeriodEndDate = limitType switch
                {
                    "daily" => DateTime.UtcNow.AddDays(1),
                    "weekly" => DateTime.UtcNow.AddDays(7),
                    "monthly" => DateTime.UtcNow.AddMonths(1),
                    _ => DateTime.UtcNow.AddDays(1)
                }
            };
            
            await _limitRepository.CreateAsync(newLimit, cancellationToken);
            
            _logger.LogInformation("Created new {LimitType} limit for user {UserId} with {MaxCredits} credits", 
                limitType, userId, maxCredits);
            
            return new UserCreditLimitDto(
                newLimit.Id,
                newLimit.LimitType,
                newLimit.MaxCredits,
                newLimit.ConsumedCredits,
                newLimit.GetRemainingCredits(),
                newLimit.PeriodStartDate,
                newLimit.PeriodEndDate,
                newLimit.IsActive,
                newLimit.ResourceType
            );
        }
    }

    public async Task RemoveUserLimitAsync(
        Guid userId, 
        string limitType, 
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        var limit = await _limitRepository.GetByUserAndTypeAsync(userId, limitType, resourceType, cancellationToken);
        
        if (limit != null)
        {
            limit.IsActive = false;
            limit.UpdatedAt = DateTime.UtcNow;
            
            await _limitRepository.UpdateAsync(limit, cancellationToken);
            
            _logger.LogInformation("Deactivated {LimitType} limit for user {UserId}", limitType, userId);
        }
    }
}