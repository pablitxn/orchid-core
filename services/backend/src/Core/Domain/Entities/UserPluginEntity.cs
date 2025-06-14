namespace Domain.Entities;

public class UserPluginEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PluginId { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // For subscription-based plugins
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public UserEntity User { get; set; } = null!;
    public PluginEntity Plugin { get; set; } = null!;
}