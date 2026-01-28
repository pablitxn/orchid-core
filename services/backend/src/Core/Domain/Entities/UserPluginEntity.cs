namespace Domain.Entities;

public class UserPluginEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PluginId { get; set; }
    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } // For subscription-based plugins
    public bool IsActive { get; set; } = true;
    public string? Settings { get; set; } // JSON-serialized plugin-specific settings

    // Version tracking
    /// <summary>
    /// Reference to the specific version the user has installed.
    /// </summary>
    public Guid? InstalledVersionId { get; set; }

    /// <summary>
    /// Denormalized version string for quick access without joins.
    /// </summary>
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// When the user last updated/changed their installed version.
    /// </summary>
    public DateTime? LastUpdatedAt { get; set; }

    /// <summary>
    /// If true, automatically update to new compatible versions.
    /// </summary>
    public bool AutoUpdate { get; set; }

    /// <summary>
    /// If set, user wants to stay on this specific version and not receive update prompts.
    /// </summary>
    public string? PinnedVersion { get; set; }

    // Navigation properties
    public UserEntity User { get; set; } = null!;
    public PluginEntity Plugin { get; set; } = null!;
    public PluginVersionEntity? InstalledVersionEntity { get; set; }
}