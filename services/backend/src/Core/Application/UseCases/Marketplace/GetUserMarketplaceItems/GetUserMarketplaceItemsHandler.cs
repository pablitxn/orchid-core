using Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Marketplace.GetUserMarketplaceItems;

public class GetUserMarketplaceItemsHandler(
    IUserPluginRepository userPluginRepository,
    IUserWorkflowRepository userWorkflowRepository,
    ILogger<GetUserMarketplaceItemsHandler> logger)
    : IRequestHandler<GetUserMarketplaceItemsQuery, UserMarketplaceItemsDto>
{
    private readonly IUserPluginRepository _userPlugins = userPluginRepository;
    private readonly IUserWorkflowRepository _userWorkflows = userWorkflowRepository;
    private readonly ILogger<GetUserMarketplaceItemsHandler> _logger = logger;

    public async Task<UserMarketplaceItemsDto> Handle(GetUserMarketplaceItemsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get user's purchased plugins
            var userPlugins = await _userPlugins.GetByUserIdAsync(request.UserId, cancellationToken);
            
            // Get user's purchased workflows
            var userWorkflows = await _userWorkflows.GetByUserIdAsync(request.UserId, cancellationToken);
            
            var result = new UserMarketplaceItemsDto
            {
                Plugins = userPlugins.Select(up => new UserPluginDto
                {
                    Id = up.Plugin.Id,
                    Name = up.Plugin.Name,
                    Description = up.Plugin.Description ?? string.Empty,
                    PriceCredits = up.Plugin.PriceCredits,
                    IsActive = up.IsActive,
                    PurchasedAt = up.PurchasedAt,
                    LastUsedAt = null, // TODO: Track plugin usage
                    UsageCount = 0 // TODO: Track plugin usage count
                }).ToList(),
                
                Workflows = userWorkflows.Select(uw => new UserWorkflowDto
                {
                    Id = uw.Workflow.Id,
                    Name = uw.Workflow.Name,
                    Description = uw.Workflow.Description,
                    PriceCredits = uw.Workflow.PriceCredits,
                    Category = uw.Workflow.Category,
                    PurchasedAt = uw.PurchasedAt,
                    LastUsedAt = null, // TODO: Track workflow execution
                    RunCount = 0 // TODO: Track workflow execution count
                }).ToList()
            };
            
            // Calculate total credits spent
            result.TotalCreditsSpent = result.Plugins.Sum(p => p.PriceCredits) + 
                                     result.Workflows.Sum(w => w.PriceCredits);
            
            _logger.LogInformation("Retrieved {PluginCount} plugins and {WorkflowCount} workflows for user {UserId}",
                result.Plugins.Count, result.Workflows.Count, request.UserId);
                
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving marketplace items for user {UserId}", request.UserId);
            throw new InvalidOperationException("Failed to retrieve your marketplace items.", ex);
        }
    }
}