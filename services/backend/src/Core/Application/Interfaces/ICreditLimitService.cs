namespace Application.Interfaces;

public interface ICreditLimitService
{
    Task<CreditLimitCheckResult> CheckLimitsAsync(
        Guid userId, 
        int requestedCredits, 
        string? resourceType = null, 
        CancellationToken cancellationToken = default);
    
    Task<bool> ConsumeLimitsAsync(
        Guid userId, 
        int credits, 
        string? resourceType = null, 
        CancellationToken cancellationToken = default);
    
    Task<UserCreditLimitsDto> GetUserLimitsAsync(
        Guid userId, 
        CancellationToken cancellationToken = default);
    
    Task<UserCreditLimitDto> SetUserLimitAsync(
        Guid userId, 
        string limitType, 
        int maxCredits, 
        string? resourceType = null,
        CancellationToken cancellationToken = default);
    
    Task RemoveUserLimitAsync(
        Guid userId, 
        string limitType, 
        string? resourceType = null,
        CancellationToken cancellationToken = default);
}

public record CreditLimitCheckResult(
    bool IsWithinLimits,
    List<LimitViolation>? Violations = null);

public record LimitViolation(
    string LimitType,
    int MaxCredits,
    int ConsumedCredits,
    int RemainingCredits,
    DateTime PeriodEndDate,
    string? ResourceType = null);

public record UserCreditLimitsDto(
    Guid UserId,
    List<UserCreditLimitDto> Limits);

public record UserCreditLimitDto(
    Guid Id,
    string LimitType,
    int MaxCredits,
    int ConsumedCredits,
    int RemainingCredits,
    DateTime PeriodStartDate,
    DateTime PeriodEndDate,
    bool IsActive,
    string? ResourceType = null);