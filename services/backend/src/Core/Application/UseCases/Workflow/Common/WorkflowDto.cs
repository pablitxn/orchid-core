using Domain.Entities;

namespace Application.UseCases.Workflow.Common;

public sealed record WorkflowDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PriceCredits { get; init; }
    public string? Author { get; init; }
    public string? Category { get; init; }
    public int Steps { get; init; }
    public string? EstimatedTime { get; init; }
    public double Rating { get; init; }
    public int Runs { get; init; }
    public string? Icon { get; init; }
    public string? Tags { get; init; }
    public string? DetailedDescription { get; init; }
    public string? Prerequisites { get; init; }
    public string? InputRequirements { get; init; }
    public string? OutputFormat { get; init; }
    public bool IsPublic { get; init; }
    public bool IsActive { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }

    public static WorkflowDto FromEntity(WorkflowEntity entity) => new()
    {
        Id = entity.Id,
        Name = entity.Name,
        Description = entity.Description,
        PriceCredits = entity.PriceCredits,
        Author = entity.Author,
        Category = entity.Category,
        Steps = entity.Steps,
        EstimatedTime = entity.EstimatedTime,
        Rating = entity.Rating,
        Runs = entity.Runs,
        Icon = entity.Icon,
        Tags = entity.Tags,
        DetailedDescription = entity.DetailedDescription,
        Prerequisites = entity.Prerequisites,
        InputRequirements = entity.InputRequirements,
        OutputFormat = entity.OutputFormat,
        IsPublic = entity.IsPublic,
        IsActive = entity.IsActive,
        PublishedAt = entity.PublishedAt,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt
    };
}
