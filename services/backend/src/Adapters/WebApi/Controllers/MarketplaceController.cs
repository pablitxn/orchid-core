using Application.UseCases.Marketplace.GetUserMarketplaceItems;
using Application.UseCases.Plugin.PurchasePlugin;
using Application.UseCases.Workflow.PurchaseWorkflow;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MarketplaceController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Gets all marketplace items (plugins and workflows) purchased by the authenticated user
    /// </summary>
    [HttpGet("user/items")]
    public async Task<IActionResult> GetUserItems()
    {
        try
        {
            var userId = User.GetUserId();
            var items = await _mediator.Send(new GetUserMarketplaceItemsQuery(userId));
            return Ok(items);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
    }
    
    /// <summary>
    /// Gets all marketplace items (plugins and workflows) purchased by the authenticated user
    /// Alternative endpoint for backward compatibility
    /// </summary>
    [HttpGet("my-items")]
    public async Task<IActionResult> GetMyItems()
    {
        return await GetUserItems();
    }
    
    /// <summary>
    /// Purchase a plugin from the marketplace
    /// </summary>
    [HttpPost("plugins/{pluginId}/purchase")]
    public async Task<IActionResult> PurchasePlugin(Guid pluginId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetUserId();
            var result = await _mediator.Send(new PurchasePluginCommand(userId, pluginId), cancellationToken);
            
            if (!result.Success)
            {
                return BadRequest(result.ErrorMessage);
            }
            
            return Ok(new { success = true, message = "Plugin purchased successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
    
    /// <summary>
    /// Purchase a workflow from the marketplace
    /// </summary>
    [HttpPost("workflows/{workflowId}/purchase")]
    public async Task<IActionResult> PurchaseWorkflow(Guid workflowId, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetUserId();
            var result = await _mediator.Send(new PurchaseWorkflowCommand(userId, workflowId), cancellationToken);
            
            if (result != PurchaseResult.Success)
            {
                var errorMessage = result switch
                {
                    PurchaseResult.WorkflowNotFound => "Workflow not found",
                    PurchaseResult.UserNotFound => "User not found",
                    PurchaseResult.InsufficientCredits => "Insufficient credits",
                    PurchaseResult.AlreadyPurchased => "You have already purchased this workflow",
                    _ => "An error occurred while processing your purchase"
                };
                return BadRequest(errorMessage);
            }
            
            return Ok(new { success = true, message = "Workflow purchased successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}