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
    public string? SystemName { get; set; } // Internal name used by Agent Framework tools

    // OpenAPI Plugin Support
    public string PluginType { get; set; } = "builtin"; // "builtin" | "openapi"
    public string? BaseUrl { get; set; } // Service base URL for HTTP calls
    public string? OpenApiSpecUrl { get; set; } // Relative path to spec (e.g., /openapi.json)
    public string? OpenApiSpecCache { get; set; } // Cached spec JSON
    public DateTime? SpecCachedAt { get; set; } // Cache timestamp
    public int? TimeoutSeconds { get; set; } // Request timeout (default 30)

    // Marketplace metadata
    public string? Author { get; set; }
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public string? Tags { get; set; } // Comma-separated tags
    public double Rating { get; set; } = 0.0;
    public int ReviewCount { get; set; } = 0;
    public int InstallCount { get; set; } = 0;
    public bool IsFeatured { get; set; } = false;
    public bool IsPublic { get; set; } = false;
    public DateTime? PublishedAt { get; set; }
    public string? Version { get; set; }
    public string? Compatibility { get; set; } // e.g., "all" or specific agent types

    // Publisher association
    public Guid? PublisherId { get; set; }
    public PublisherEntity? Publisher { get; set; }

    // Version management
    /// <summary>
    /// Reference to the latest published version.
    /// </summary>
    public Guid? LatestVersionId { get; set; }

    /// <summary>
    /// Total number of versions published for this plugin.
    /// </summary>
    public int VersionCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties for versions
    public ICollection<PluginVersionEntity> Versions { get; set; } = new List<PluginVersionEntity>();
}