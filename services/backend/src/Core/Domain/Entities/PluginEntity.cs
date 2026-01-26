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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}