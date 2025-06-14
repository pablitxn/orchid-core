namespace Domain.Entities;

public class CostConfigurationEntity
{
    public Guid Id { get; init; }
    
    // Type of cost: "message_fixed", "message_token", "plugin_usage", "plugin_purchase", "workflow_usage", "workflow_purchase"
    public string CostType { get; set; } = string.Empty;
    
    // Optional: Specific resource ID (for plugin/workflow specific costs)
    public Guid? ResourceId { get; set; }
    
    // Optional: Resource name for easier identification
    public string? ResourceName { get; set; }
    
    // Cost in credits
    public int CreditCost { get; set; }
    
    // For token-based costs: cost per 1000 tokens
    public decimal? CostPer1kTokens { get; set; }
    
    // Is this configuration active?
    public bool IsActive { get; set; } = true;
    
    // Priority for override rules (higher priority wins)
    public int Priority { get; set; } = 0;
    
    // Optional: Effective date range
    public DateTime? EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    
    // Metadata
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    
    // JSON metadata for additional configuration
    public string? Metadata { get; set; }
    
    public bool IsEffective(DateTime? date = null)
    {
        var checkDate = date ?? DateTime.UtcNow;
        
        if (!IsActive) return false;
        if (EffectiveFrom.HasValue && checkDate < EffectiveFrom.Value) return false;
        if (EffectiveTo.HasValue && checkDate > EffectiveTo.Value) return false;
        
        return true;
    }
}