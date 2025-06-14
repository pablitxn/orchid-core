using Application.Common.Models;
using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed class ListPublicWorkflowsHandler(
    IWorkflowRepository workflowRepository,
    ILogger<ListPublicWorkflowsHandler> logger
) : IRequestHandler<ListPublicWorkflowsQuery, ListPublicWorkflowsResult>
{
    private readonly IWorkflowRepository _workflowRepository = workflowRepository;
    private readonly ILogger<ListPublicWorkflowsHandler> _logger = logger;

    public async Task<ListPublicWorkflowsResult> Handle(ListPublicWorkflowsQuery query, CancellationToken cancellationToken)
    {
        // Get all workflows (in a real implementation, this would be filtered at the database level)
        var allWorkflows = await _workflowRepository.ListAsync(cancellationToken);
        
        // Filter only public, active workflows with published date
        var filteredWorkflows = allWorkflows
            .Where(w => w.IsPublic && w.IsActive && w.PublishedAt != null)
            .AsQueryable();
        
        // Apply search filter
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchLower = query.SearchTerm.ToLowerInvariant();
            filteredWorkflows = filteredWorkflows.Where(w => 
                (w.Name != null && w.Name.ToLowerInvariant().Contains(searchLower)) ||
                (w.Description != null && w.Description.ToLowerInvariant().Contains(searchLower)) ||
                (w.Tags != null && w.Tags.ToLowerInvariant().Contains(searchLower)));
        }
        
        // Apply category filter
        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            filteredWorkflows = filteredWorkflows.Where(w => 
                w.Category != null && w.Category.Equals(query.Category, StringComparison.OrdinalIgnoreCase));
        }
        
        // Apply price filters
        if (query.MinPrice.HasValue)
        {
            filteredWorkflows = filteredWorkflows.Where(w => w.PriceCredits >= query.MinPrice.Value);
        }
        
        if (query.MaxPrice.HasValue)
        {
            filteredWorkflows = filteredWorkflows.Where(w => w.PriceCredits <= query.MaxPrice.Value);
        }
        
        // Count before pagination
        var totalCount = filteredWorkflows.Count();
        
        // Apply sorting
        filteredWorkflows = ApplySorting(filteredWorkflows, query.SortBy, query.SortDescending);
        
        // Apply pagination
        var paginatedWorkflows = filteredWorkflows
            .Skip(query.Skip)
            .Take(query.Take)
            .Select(w => new PublicWorkflow(
                w.Id,
                w.Name,
                w.Description,
                w.PriceCredits,
                w.Author,
                w.Category,
                w.Steps,
                w.EstimatedTime,
                w.Rating,
                w.Runs,
                w.Icon,
                w.Tags,
                w.CreatedAt,
                w.UpdatedAt,
                w.PublishedAt
            ))
            .ToList();
        
        var paginatedResult = new PaginatedResult<PublicWorkflow>(
            paginatedWorkflows,
            totalCount,
            query.Page,
            query.PageSize);
        
        _logger.LogInformation("Listed {Count} public workflows out of {Total} (page {Page})", 
            paginatedWorkflows.Count, totalCount, query.Page);
        
        return new ListPublicWorkflowsResult(paginatedResult);
    }
    
    private IQueryable<Domain.Entities.WorkflowEntity> ApplySorting(
        IQueryable<Domain.Entities.WorkflowEntity> query,
        WorkflowSortBy sortBy,
        bool descending)
    {
        return sortBy switch
        {
            WorkflowSortBy.Name => descending 
                ? query.OrderByDescending(w => w.Name) 
                : query.OrderBy(w => w.Name),
                
            WorkflowSortBy.Price => descending 
                ? query.OrderByDescending(w => w.PriceCredits) 
                : query.OrderBy(w => w.PriceCredits),
                
            WorkflowSortBy.Rating => descending 
                ? query.OrderByDescending(w => w.Rating).ThenByDescending(w => w.Runs)
                : query.OrderBy(w => w.Rating).ThenBy(w => w.Runs),
                
            WorkflowSortBy.Runs => descending 
                ? query.OrderByDescending(w => w.Runs) 
                : query.OrderBy(w => w.Runs),
                
            WorkflowSortBy.CreatedAt => descending 
                ? query.OrderByDescending(w => w.CreatedAt) 
                : query.OrderBy(w => w.CreatedAt),
                
            _ => query.OrderByDescending(w => w.Rating)
        };
    }
}