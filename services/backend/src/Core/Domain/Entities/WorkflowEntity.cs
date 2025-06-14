namespace Domain.Entities;

public class WorkflowEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PriceCredits { get; set; }
    public string? Author { get; set; }
    public string? Category { get; set; }
    public int Steps { get; set; } = 1;
    public string? EstimatedTime { get; set; }
    public double Rating { get; set; } = 0.0;
    public int Runs { get; set; } = 0;
    public string? Icon { get; set; }
    public string? Tags { get; set; } // Comma-separated tags
    public string? DetailedDescription { get; set; }
    public string? Prerequisites { get; set; }
    public string? InputRequirements { get; set; }
    public string? OutputFormat { get; set; }
    public bool IsPublic { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
