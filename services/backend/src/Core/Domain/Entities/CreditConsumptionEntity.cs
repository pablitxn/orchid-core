namespace Domain.Entities;

public class CreditConsumptionEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    
    // Type of consumption: "plugin", "workflow", "message"
    public string ConsumptionType { get; set; } = string.Empty;
    
    // ID of the consumed resource (plugin, workflow, or message)
    public Guid? ResourceId { get; set; }
    
    // Name of the resource for display purposes
    public string ResourceName { get; set; } = string.Empty;
    
    // Amount of credits consumed
    public int CreditsConsumed { get; set; }
    
    // Additional metadata (e.g., plugin parameters, workflow config)
    public string? Metadata { get; set; }
    
    // Timestamp of consumption
    public DateTime ConsumedAt { get; set; } = DateTime.UtcNow;
    
    // Balance after consumption
    public int BalanceAfter { get; set; }
}