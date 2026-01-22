namespace Domain.Entities;

public class FeatureFlagEntity
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? AllowedPlans { get; set; }  // Comma-separated plan names (e.g., "pro,enterprise")
    public int RolloutPercentage { get; set; } = 100;  // 0-100
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
