using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CostRegistryService : ICostRegistry
{
    private readonly ICostConfigurationRepository _costConfigRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CostRegistryService> _logger;
    
    // Default costs if not configured
    private const int DEFAULT_MESSAGE_FIXED_COST = 5;
    private const decimal DEFAULT_MESSAGE_TOKEN_COST_PER_1K = 1.0m;
    private const int DEFAULT_PLUGIN_PURCHASE_COST = 50;
    private const int DEFAULT_PLUGIN_USAGE_COST = 10;
    private const int DEFAULT_WORKFLOW_PURCHASE_COST = 100;
    private const int DEFAULT_WORKFLOW_USAGE_COST = 20;
    
    private const string CACHE_KEY_PREFIX = "cost_config:";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

    public CostRegistryService(
        ICostConfigurationRepository costConfigRepository,
        IMemoryCache cache,
        ILogger<CostRegistryService> logger)
    {
        _costConfigRepository = costConfigRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<int> GetMessageFixedCostAsync(CancellationToken cancellationToken = default)
    {
        return await GetCachedCostAsync("message_fixed", null, DEFAULT_MESSAGE_FIXED_COST, cancellationToken);
    }

    public async Task<decimal> GetMessageTokenCostPer1kAsync(CancellationToken cancellationToken = default)
    {
        var config = await GetCostConfigurationAsync("message_token", null, cancellationToken);
        return config?.CostPer1kTokens ?? DEFAULT_MESSAGE_TOKEN_COST_PER_1K;
    }

    public async Task<int> GetPluginPurchaseCostAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        return await GetCachedCostAsync("plugin_purchase", pluginId, DEFAULT_PLUGIN_PURCHASE_COST, cancellationToken);
    }

    public async Task<int> GetPluginUsageCostAsync(Guid pluginId, CancellationToken cancellationToken = default)
    {
        return await GetCachedCostAsync("plugin_usage", pluginId, DEFAULT_PLUGIN_USAGE_COST, cancellationToken);
    }

    public async Task<int> GetWorkflowPurchaseCostAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        return await GetCachedCostAsync("workflow_purchase", workflowId, DEFAULT_WORKFLOW_PURCHASE_COST, cancellationToken);
    }

    public async Task<int> GetWorkflowUsageCostAsync(Guid workflowId, CancellationToken cancellationToken = default)
    {
        return await GetCachedCostAsync("workflow_usage", workflowId, DEFAULT_WORKFLOW_USAGE_COST, cancellationToken);
    }

    public async Task<int> GetCostAsync(string costType, Guid? resourceId = null, CancellationToken cancellationToken = default)
    {
        var defaultCost = costType switch
        {
            "message_fixed" => DEFAULT_MESSAGE_FIXED_COST,
            "plugin_purchase" => DEFAULT_PLUGIN_PURCHASE_COST,
            "plugin_usage" => DEFAULT_PLUGIN_USAGE_COST,
            "workflow_purchase" => DEFAULT_WORKFLOW_PURCHASE_COST,
            "workflow_usage" => DEFAULT_WORKFLOW_USAGE_COST,
            _ => 0
        };
        
        return await GetCachedCostAsync(costType, resourceId, defaultCost, cancellationToken);
    }

    public async Task<Dictionary<Guid, int>> GetPluginUsageCostsBatchAsync(
        IEnumerable<Guid> pluginIds, 
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, int>();
        
        foreach (var pluginId in pluginIds)
        {
            result[pluginId] = await GetPluginUsageCostAsync(pluginId, cancellationToken);
        }
        
        return result;
    }

    public async Task<Dictionary<Guid, int>> GetWorkflowUsageCostsBatchAsync(
        IEnumerable<Guid> workflowIds, 
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<Guid, int>();
        
        foreach (var workflowId in workflowIds)
        {
            result[workflowId] = await GetWorkflowUsageCostAsync(workflowId, cancellationToken);
        }
        
        return result;
    }

    private async Task<int> GetCachedCostAsync(
        string costType, 
        Guid? resourceId, 
        int defaultCost,
        CancellationToken cancellationToken)
    {
        var config = await GetCostConfigurationAsync(costType, resourceId, cancellationToken);
        return config?.CreditCost ?? defaultCost;
    }

    private async Task<CostConfigurationEntity?> GetCostConfigurationAsync(
        string costType, 
        Guid? resourceId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{CACHE_KEY_PREFIX}{costType}:{resourceId?.ToString() ?? "default"}";
        
        if (_cache.TryGetValue<CostConfigurationEntity>(cacheKey, out var cachedConfig))
        {
            return cachedConfig;
        }

        try
        {
            var config = await _costConfigRepository.GetCostForTypeAsync(costType, resourceId, cancellationToken);
            
            if (config != null && config.IsEffective())
            {
                _cache.Set(cacheKey, config, CACHE_DURATION);
                return config;
            }
            
            // Try to get default configuration for this type (without specific resourceId)
            if (resourceId.HasValue)
            {
                config = await _costConfigRepository.GetCostForTypeAsync(costType, null, cancellationToken);
                if (config != null && config.IsEffective())
                {
                    _cache.Set(cacheKey, config, CACHE_DURATION);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cost configuration for type {CostType} and resource {ResourceId}", 
                costType, resourceId);
        }

        return null;
    }
}