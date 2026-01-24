using Application.Common.Models;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed record ListPublicWorkflowDtosDto(PaginatedResult<PublicWorkflowDto> Workflows);

public sealed record PublicWorkflowDto(
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