namespace Domain.Entities;

public class PluginEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SourceUrl { get; set; }
    public bool IsActive { get; set; }
    public int PriceCredits { get; set; } = 10; // Default price in credits
    public bool IsSubscriptionBased { get; set; } = false; // If true, requires active subscription
    public string? SystemName { get; set; } // Internal name used by Semantic Kernel
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}