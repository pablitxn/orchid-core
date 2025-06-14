namespace Domain.Entities;

public class UserCreditLimitEntity
{
    public Guid Id { get; init; }
    
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    
    // Limit type: "daily", "weekly", "monthly"
    public string LimitType { get; set; } = string.Empty;
    
    // Maximum credits allowed in the period
    public int MaxCredits { get; set; }
    
    // Credits consumed in the current period
    public int ConsumedCredits { get; set; } = 0;
    
    // When the current period started
    public DateTime PeriodStartDate { get; set; }
    
    // When the current period ends
    public DateTime PeriodEndDate { get; set; }
    
    // Is this limit active?
    public bool IsActive { get; set; } = true;
    
    // Optional: Specific resource type this limit applies to (null = all)
    public string? ResourceType { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    
    // Concurrency control
    public int Version { get; set; } = 0;
    
    public bool IsWithinLimit(int requestedCredits)
    {
        if (!IsActive) return true;
        
        // Check if period has expired and needs reset
        if (DateTime.UtcNow > PeriodEndDate)
        {
            ResetPeriod();
        }
        
        return ConsumedCredits + requestedCredits <= MaxCredits;
    }
    
    public void ConsumeCredits(int credits)
    {
        if (DateTime.UtcNow > PeriodEndDate)
        {
            ResetPeriod();
        }
        
        ConsumedCredits += credits;
        UpdatedAt = DateTime.UtcNow;
    }
    
    public int GetRemainingCredits()
    {
        if (DateTime.UtcNow > PeriodEndDate)
        {
            return MaxCredits;
        }
        
        return Math.Max(0, MaxCredits - ConsumedCredits);
    }
    
    private void ResetPeriod()
    {
        PeriodStartDate = DateTime.UtcNow;
        
        PeriodEndDate = LimitType switch
        {
            "daily" => PeriodStartDate.AddDays(1),
            "weekly" => PeriodStartDate.AddDays(7),
            "monthly" => PeriodStartDate.AddMonths(1),
            _ => PeriodStartDate.AddDays(1)
        };
        
        ConsumedCredits = 0;
        UpdatedAt = DateTime.UtcNow;
    }
}