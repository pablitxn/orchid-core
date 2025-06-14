namespace Application.Interfaces;

public interface ICostRegistry
{
    // Message costs
    Task<int> GetMessageFixedCostAsync(CancellationToken cancellationToken = default);
    Task<decimal> GetMessageTokenCostPer1kAsync(CancellationToken cancellationToken = default);
    
    // Plugin costs
    Task<int> GetPluginPurchaseCostAsync(Guid pluginId, CancellationToken cancellationToken = default);
    Task<int> GetPluginUsageCostAsync(Guid pluginId, CancellationToken cancellationToken = default);
    
    // Workflow costs
    Task<int> GetWorkflowPurchaseCostAsync(Guid workflowId, CancellationToken cancellationToken = default);
    Task<int> GetWorkflowUsageCostAsync(Guid workflowId, CancellationToken cancellationToken = default);
    
    // Generic cost lookup
    Task<int> GetCostAsync(string costType, Guid? resourceId = null, CancellationToken cancellationToken = default);
    
    // Batch operations for performance
    Task<Dictionary<Guid, int>> GetPluginUsageCostsBatchAsync(IEnumerable<Guid> pluginIds, CancellationToken cancellationToken = default);
    Task<Dictionary<Guid, int>> GetWorkflowUsageCostsBatchAsync(IEnumerable<Guid> workflowIds, CancellationToken cancellationToken = default);
}