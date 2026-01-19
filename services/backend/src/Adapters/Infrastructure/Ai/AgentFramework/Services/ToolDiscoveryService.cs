using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Discovers available tools from the ToolRegistry for plugin seeding.
/// </summary>
public class ToolDiscoveryService : IPluginDiscoveryService
{
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<ToolDiscoveryService> _logger;

    public ToolDiscoveryService(
        IToolRegistry toolRegistry,
        ILogger<ToolDiscoveryService> logger)
    {
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<DiscoveredPlugin>> DiscoverPluginsAsync(
        CancellationToken cancellationToken = default)
    {
        var tools = _toolRegistry.GetAllTools();
        var plugins = new List<DiscoveredPlugin>();

        foreach (var tool in tools)
        {
            try
            {
                var functions = tool.GetAIFunctions()
                    .Select(f => f.Name)
                    .ToList();

                var plugin = new DiscoveredPlugin(
                    Name: tool.DisplayName,
                    SystemName: tool.SystemName,
                    Description: tool.Description,
                    Functions: functions,
                    DefaultPriceCredits: tool.CreditCost,
                    IsSubscriptionBased: tool.RequiresSubscription);

                plugins.Add(plugin);

                _logger.LogDebug(
                    "Discovered tool {SystemName} with {FunctionCount} functions",
                    tool.SystemName,
                    functions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to discover functions for tool {SystemName}",
                    tool.SystemName);
            }
        }

        _logger.LogInformation("Discovered {PluginCount} tools", plugins.Count);

        return Task.FromResult<IEnumerable<DiscoveredPlugin>>(plugins);
    }
}
