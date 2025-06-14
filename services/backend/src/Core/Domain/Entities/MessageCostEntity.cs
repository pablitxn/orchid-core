namespace Domain.Entities;

public class MessageCostEntity
{
    public Guid Id { get; set; }
    public Guid MessageId { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    
    // Billing method: "tokens" or "fixed"
    public string BillingMethod { get; set; } = "tokens";
    
    // For token-based billing
    public int? TokensConsumed { get; set; }
    public decimal? CostPerToken { get; set; }
    
    // For fixed-rate billing
    public decimal? FixedRate { get; set; }
    
    // Total cost in credits
    public int TotalCredits { get; set; }
    
    // If the message used plugins/workflows
    public bool HasPluginUsage { get; set; }
    public bool HasWorkflowUsage { get; set; }
    
    // Additional plugin/workflow costs (separate from message cost)
    public int AdditionalCredits { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}