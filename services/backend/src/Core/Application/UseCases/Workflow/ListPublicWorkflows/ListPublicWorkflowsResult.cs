using Application.Common.Models;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed record ListPublicWorkflowsResult(PaginatedResult<PublicWorkflow> Workflows);

public sealed record PublicWorkflow(
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
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? PublishedAt
);