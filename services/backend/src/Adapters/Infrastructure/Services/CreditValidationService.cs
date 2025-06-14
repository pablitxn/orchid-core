using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CreditValidationService : ICreditValidationService
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ICostRegistry _costRegistry;
    private readonly IPluginRepository _pluginRepository;
    private readonly IWorkflowRepository _workflowRepository;
    private readonly IUserBillingPreferenceRepository _billingPreferenceRepository;
    private readonly ILogger<CreditValidationService> _logger;

    public CreditValidationService(
        ISubscriptionRepository subscriptionRepository,
        ICostRegistry costRegistry,
        IPluginRepository pluginRepository,
        IWorkflowRepository workflowRepository,
        IUserBillingPreferenceRepository billingPreferenceRepository,
        ILogger<CreditValidationService> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _costRegistry = costRegistry;
        _pluginRepository = pluginRepository;
        _workflowRepository = workflowRepository;
        _billingPreferenceRepository = billingPreferenceRepository;
        _logger = logger;
    }

    public async Task<CreditValidationResult> ValidateMessageCostAsync(
        Guid userId,
        string messageContent,
        IEnumerable<Guid>? pluginIds = null,
        IEnumerable<Guid>? workflowIds = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (subscription == null)
            {
                return new CreditValidationResult(false, 0, 0, false, "No active subscription found");
            }

            // Check if user has unlimited credits
            if (subscription.HasUnlimitedCredits())
            {
                return new CreditValidationResult(true, 0, int.MaxValue, true);
            }

            // Get billing preferences
            var billingPreference = await _billingPreferenceRepository.GetByUserIdAsync(userId, cancellationToken);
            
            // Calculate base message cost
            int baseCost = 0;
            if (billingPreference?.MessageBillingMethod == "tokens")
            {
                var tokenCostPer1k = await _costRegistry.GetMessageTokenCostPer1kAsync(cancellationToken);
                var estimatedTokens = EstimateTokenCount(messageContent);
                baseCost = (int)Math.Ceiling((estimatedTokens / 1000.0m) * (decimal)tokenCostPer1k);
            }
            else
            {
                baseCost = await _costRegistry.GetMessageFixedCostAsync(cancellationToken);
            }

            // Calculate plugin costs
            var pluginCosts = new Dictionary<string, int>();
            if (pluginIds != null && pluginIds.Any())
            {
                var pluginCostMap = await _costRegistry.GetPluginUsageCostsBatchAsync(pluginIds, cancellationToken);
                foreach (var (pluginId, cost) in pluginCostMap)
                {
                    var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
                    if (plugin != null)
                    {
                        pluginCosts[plugin.Name] = cost;
                    }
                }
            }

            // Calculate workflow costs
            var workflowCosts = new Dictionary<string, int>();
            if (workflowIds != null && workflowIds.Any())
            {
                var workflowCostMap = await _costRegistry.GetWorkflowUsageCostsBatchAsync(workflowIds, cancellationToken);
                foreach (var (workflowId, cost) in workflowCostMap)
                {
                    var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
                    if (workflow != null)
                    {
                        workflowCosts[workflow.Name] = cost;
                    }
                }
            }

            var totalCost = baseCost + pluginCosts.Values.Sum() + workflowCosts.Values.Sum();
            var costBreakdown = new CostBreakdown(baseCost, pluginCosts, workflowCosts, totalCost);

            if (subscription.Credits < totalCost)
            {
                return new CreditValidationResult(
                    false, 
                    totalCost, 
                    subscription.Credits, 
                    false,
                    $"Insufficient credits. Required: {totalCost}, Available: {subscription.Credits}",
                    costBreakdown);
            }

            return new CreditValidationResult(true, totalCost, subscription.Credits, false, null, costBreakdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating message cost for user {UserId}", userId);
            return new CreditValidationResult(false, 0, 0, false, "An error occurred while validating credits");
        }
    }

    public async Task<CreditValidationResult> ValidatePluginPurchaseAsync(
        Guid userId,
        Guid pluginId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (subscription == null)
            {
                return new CreditValidationResult(false, 0, 0, false, "No active subscription found");
            }

            if (subscription.HasUnlimitedCredits())
            {
                return new CreditValidationResult(true, 0, int.MaxValue, true);
            }

            var plugin = await _pluginRepository.GetByIdAsync(pluginId, cancellationToken);
            if (plugin == null)
            {
                return new CreditValidationResult(false, 0, subscription.Credits, false, "Plugin not found");
            }

            if (plugin.IsSubscriptionBased)
            {
                // Subscription-based plugins don't require credits
                return new CreditValidationResult(true, 0, subscription.Credits, false);
            }

            var cost = await _costRegistry.GetPluginPurchaseCostAsync(pluginId, cancellationToken);
            
            if (subscription.Credits < cost)
            {
                return new CreditValidationResult(
                    false, 
                    cost, 
                    subscription.Credits, 
                    false,
                    $"Insufficient credits. Required: {cost}, Available: {subscription.Credits}");
            }

            return new CreditValidationResult(true, cost, subscription.Credits, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating plugin purchase for user {UserId}, plugin {PluginId}", userId, pluginId);
            return new CreditValidationResult(false, 0, 0, false, "An error occurred while validating credits");
        }
    }

    public async Task<CreditValidationResult> ValidateWorkflowPurchaseAsync(
        Guid userId,
        Guid workflowId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (subscription == null)
            {
                return new CreditValidationResult(false, 0, 0, false, "No active subscription found");
            }

            if (subscription.HasUnlimitedCredits())
            {
                return new CreditValidationResult(true, 0, int.MaxValue, true);
            }

            var workflow = await _workflowRepository.GetByIdAsync(workflowId, cancellationToken);
            if (workflow == null)
            {
                return new CreditValidationResult(false, 0, subscription.Credits, false, "Workflow not found");
            }

            var cost = await _costRegistry.GetWorkflowPurchaseCostAsync(workflowId, cancellationToken);
            
            if (subscription.Credits < cost)
            {
                return new CreditValidationResult(
                    false, 
                    cost, 
                    subscription.Credits, 
                    false,
                    $"Insufficient credits. Required: {cost}, Available: {subscription.Credits}");
            }

            return new CreditValidationResult(true, cost, subscription.Credits, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating workflow purchase for user {UserId}, workflow {WorkflowId}", userId, workflowId);
            return new CreditValidationResult(false, 0, 0, false, "An error occurred while validating credits");
        }
    }

    public async Task<CreditValidationResult> ValidateOperationAsync(
        Guid userId,
        int requiredCredits,
        string operationType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var subscription = await _subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (subscription == null)
            {
                return new CreditValidationResult(false, requiredCredits, 0, false, "No active subscription found");
            }

            if (subscription.HasUnlimitedCredits())
            {
                return new CreditValidationResult(true, requiredCredits, int.MaxValue, true);
            }

            if (subscription.Credits < requiredCredits)
            {
                return new CreditValidationResult(
                    false, 
                    requiredCredits, 
                    subscription.Credits, 
                    false,
                    $"Insufficient credits for {operationType}. Required: {requiredCredits}, Available: {subscription.Credits}");
            }

            return new CreditValidationResult(true, requiredCredits, subscription.Credits, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating operation {OperationType} for user {UserId}", operationType, userId);
            return new CreditValidationResult(false, requiredCredits, 0, false, "An error occurred while validating credits");
        }
    }

    private int EstimateTokenCount(string content)
    {
        // Simple estimation: ~4 characters per token (rough approximation)
        return Math.Max(1, content.Length / 4);
    }
}