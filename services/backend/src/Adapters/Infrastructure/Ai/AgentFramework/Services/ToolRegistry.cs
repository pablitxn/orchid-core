using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Central registry for all available tools in the Agent Framework.
/// Resolves tools from DI and provides lookup by system name.
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolRegistry> _logger;
    private readonly Dictionary<string, Func<ITool>> _toolFactories;

    public ToolRegistry(
        IServiceProvider serviceProvider,
        ILogger<ToolRegistry> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Register tool factories with normalized names
        _toolFactories = new Dictionary<string, Func<ITool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["vector_store"] = () => _serviceProvider.GetRequiredService<VectorStoreTool>(),
            ["vectorstore"] = () => _serviceProvider.GetRequiredService<VectorStoreTool>(),
            ["web_search"] = () => _serviceProvider.GetRequiredService<WebSearchTool>(),
            ["websearch"] = () => _serviceProvider.GetRequiredService<WebSearchTool>()
        };
    }

    public ITool? GetToolBySystemName(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            _logger.LogWarning("Attempted to get tool with null or empty system name");
            return null;
        }

        var normalizedName = systemName.Trim().ToLowerInvariant();

        if (_toolFactories.TryGetValue(normalizedName, out var factory))
        {
            try
            {
                var tool = factory();
                _logger.LogDebug("Resolved tool '{SystemName}' → {ToolType}", systemName, tool.GetType().Name);
                return tool;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve tool '{SystemName}'", systemName);
                return null;
            }
        }

        _logger.LogWarning("Unknown tool system name: '{SystemName}'", systemName);
        return null;
    }

    public IReadOnlyList<AIFunction> GetAIFunctionsBySystemName(string systemName)
    {
        var tool = GetToolBySystemName(systemName);
        if (tool == null)
        {
            return Array.Empty<AIFunction>();
        }

        return tool.GetAIFunctions();
    }

    public IReadOnlyCollection<ITool> GetAllTools()
    {
        // Return unique tools (avoid duplicates from aliases)
        var uniqueTools = new Dictionary<string, ITool>();

        foreach (var kvp in _toolFactories)
        {
            try
            {
                var tool = kvp.Value();
                if (!uniqueTools.ContainsKey(tool.SystemName))
                {
                    uniqueTools[tool.SystemName] = tool;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enumerate tool for key '{Key}'", kvp.Key);
            }
        }

        return uniqueTools.Values;
    }

    public bool IsToolRegistered(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return false;
        }

        var normalizedName = systemName.Trim().ToLowerInvariant();
        return _toolFactories.ContainsKey(normalizedName);
    }
}
