using Application.Common.Models;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed class ListPublicWorkflowsQuery : PaginatedQuery, IRequest<ListPublicWorkflowsResult>
{
    public string? Category { get; set; }
    public string? SearchTerm { get; set; }
    public WorkflowSortBy SortBy { get; set; } = WorkflowSortBy.Rating;
    public bool SortDescending { get; set; } = true;
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

public enum WorkflowSortBy
{
    Name,
    Price,
    Rating,
    Runs,
    CreatedAt
}