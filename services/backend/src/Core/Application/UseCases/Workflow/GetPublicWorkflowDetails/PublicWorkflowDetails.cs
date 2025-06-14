namespace Application.UseCases.Workflow.ListWorkflows;

public sealed record PublicWorkflowDetails(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    string? Author,
    string? Category,
    int Steps,
    string? EstimatedTime,
    double Rating,
    int Runs,
    string? Icon,
    string? Tags,
    string? DetailedDescription,
    string? Prerequisites,
    string? InputRequirements,
    string? OutputFormat,
    DateTime CreatedAt,
    DateTime UpdatedAt
);