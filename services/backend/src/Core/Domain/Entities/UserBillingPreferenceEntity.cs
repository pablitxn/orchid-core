namespace Domain.Entities;

public class UserBillingPreferenceEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    
    // Message billing preference: "tokens" or "fixed"
    public string MessageBillingMethod { get; set; } = "tokens";
    
    // Rate configurations
    public decimal TokenRate { get; set; } = 0.001m; // Default credits per token
    public int FixedMessageRate { get; set; } = 5; // Default credits per message
    
    // Alert thresholds
    public int? LowCreditThreshold { get; set; } = 100; // Alert when credits below this
    public bool EnableLowCreditAlerts { get; set; } = true;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}