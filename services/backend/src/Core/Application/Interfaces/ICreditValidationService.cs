namespace Application.Interfaces;

public interface ICreditValidationService
{
    Task<CreditValidationResult> ValidateMessageCostAsync(
        Guid userId,
        string messageContent,
        IEnumerable<Guid>? pluginIds = null,
        IEnumerable<Guid>? workflowIds = null,
        CancellationToken cancellationToken = default);
    
    Task<CreditValidationResult> ValidatePluginPurchaseAsync(
        Guid userId,
        Guid pluginId,
        CancellationToken cancellationToken = default);
    
    Task<CreditValidationResult> ValidateWorkflowPurchaseAsync(
        Guid userId,
        Guid workflowId,
        CancellationToken cancellationToken = default);
    
    Task<CreditValidationResult> ValidateOperationAsync(
        Guid userId,
        int requiredCredits,
        string operationType,
        CancellationToken cancellationToken = default);
}

public record CreditValidationResult(
    bool IsValid,
    int RequiredCredits,
    int AvailableCredits,
    bool HasUnlimitedCredits,
    string? ErrorMessage = null,
    CostBreakdown? CostBreakdown = null);

public record CostBreakdown(
    int BaseCost,
    Dictionary<string, int> PluginCosts,
    Dictionary<string, int> WorkflowCosts,
    int TotalCost);