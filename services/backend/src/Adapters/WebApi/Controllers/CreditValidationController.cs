using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace WebApi.Controllers;

[ApiController]
[Route("api/credit-validation")]
[Authorize]
public class CreditValidationController : ControllerBase
{
    private readonly ICreditValidationService _creditValidationService;
    private readonly ILogger<CreditValidationController> _logger;

    public CreditValidationController(
        ICreditValidationService creditValidationService,
        ILogger<CreditValidationController> logger)
    {
        _creditValidationService = creditValidationService;
        _logger = logger;
    }

    [HttpPost("message/validate")]
    public async Task<IActionResult> ValidateMessageCost(
        [FromBody] ValidateMessageRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var result = await _creditValidationService.ValidateMessageCostAsync(
            userId,
            request.MessageContent,
            request.PluginIds,
            request.WorkflowIds,
            cancellationToken);

        return Ok(new
        {
            isValid = result.IsValid,
            requiredCredits = result.RequiredCredits,
            availableCredits = result.AvailableCredits,
            hasUnlimitedCredits = result.HasUnlimitedCredits,
            errorMessage = result.ErrorMessage,
            costBreakdown = result.CostBreakdown != null ? new
            {
                baseCost = result.CostBreakdown.BaseCost,
                pluginCosts = result.CostBreakdown.PluginCosts,
                workflowCosts = result.CostBreakdown.WorkflowCosts,
                totalCost = result.CostBreakdown.TotalCost
            } : null
        });
    }

    [HttpPost("plugin/{pluginId}/validate")]
    public async Task<IActionResult> ValidatePluginPurchase(
        Guid pluginId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var result = await _creditValidationService.ValidatePluginPurchaseAsync(
            userId,
            pluginId,
            cancellationToken);

        return Ok(new
        {
            isValid = result.IsValid,
            requiredCredits = result.RequiredCredits,
            availableCredits = result.AvailableCredits,
            hasUnlimitedCredits = result.HasUnlimitedCredits,
            errorMessage = result.ErrorMessage
        });
    }

    [HttpPost("workflow/{workflowId}/validate")]
    public async Task<IActionResult> ValidateWorkflowPurchase(
        Guid workflowId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var result = await _creditValidationService.ValidateWorkflowPurchaseAsync(
            userId,
            workflowId,
            cancellationToken);

        return Ok(new
        {
            isValid = result.IsValid,
            requiredCredits = result.RequiredCredits,
            availableCredits = result.AvailableCredits,
            hasUnlimitedCredits = result.HasUnlimitedCredits,
            errorMessage = result.ErrorMessage
        });
    }

    [HttpPost("operation/validate")]
    public async Task<IActionResult> ValidateOperation(
        [FromBody] ValidateOperationRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        
        var result = await _creditValidationService.ValidateOperationAsync(
            userId,
            request.RequiredCredits,
            request.OperationType,
            cancellationToken);

        return Ok(new
        {
            isValid = result.IsValid,
            requiredCredits = result.RequiredCredits,
            availableCredits = result.AvailableCredits,
            hasUnlimitedCredits = result.HasUnlimitedCredits,
            errorMessage = result.ErrorMessage
        });
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("Invalid user ID in token");
        }
        return userId;
    }
}

public record ValidateMessageRequest(
    string MessageContent,
    List<Guid>? PluginIds,
    List<Guid>? WorkflowIds);

public record ValidateOperationRequest(
    int RequiredCredits,
    string OperationType);