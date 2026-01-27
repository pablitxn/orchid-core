using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Central registry for all available tools in the Agent Framework.
/// Resolves builtin tools from DI and OpenAPI tools from the database.
/// Provides both sync methods (for builtin tools) and async methods (for all tools).
/// </summary>
public class ToolRegistry : IToolRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ToolRegistry> _logger;
    private readonly IOpenApiToolFactory? _openApiToolFactory;
    private readonly IPluginRepository? _pluginRepository;
    private readonly Dictionary<string, Func<ITool>> _toolFactories;

    public ToolRegistry(
        IServiceProvider serviceProvider,
        ILogger<ToolRegistry> logger,
        IOpenApiToolFactory? openApiToolFactory = null,
        IPluginRepository? pluginRepository = null)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openApiToolFactory = openApiToolFactory;
        _pluginRepository = pluginRepository;

        // Register tool factories with normalized names
        _toolFactories = new Dictionary<string, Func<ITool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["vector_store"] = () => _serviceProvider.GetRequiredService<VectorStoreTool>(),
            ["vectorstore"] = () => _serviceProvider.GetRequiredService<VectorStoreTool>(),
            ["web_search"] = () => _serviceProvider.GetRequiredService<WebSearchTool>(),
            ["websearch"] = () => _serviceProvider.GetRequiredService<WebSearchTool>()
        };
    }

    /// <inheritdoc />
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
                _logger.LogDebug("Resolved builtin tool '{SystemName}' → {ToolType}", systemName, tool.GetType().Name);
                return tool;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve builtin tool '{SystemName}'", systemName);
                return null;
            }
        }

        _logger.LogDebug("Builtin tool not found: '{SystemName}'", systemName);
        return null;
    }

    /// <inheritdoc />
    public async Task<ITool?> GetToolBySystemNameAsync(string systemName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            _logger.LogWarning("Attempted to get tool with null or empty system name");
            return null;
        }

        // 1. Fast path: check builtin tools first (sync)
        var builtinTool = GetToolBySystemName(systemName);
        if (builtinTool != null)
        {
            return builtinTool;
        }

        // 2. Check OpenAPI tools from database (async)
        if (_pluginRepository == null || _openApiToolFactory == null)
        {
            _logger.LogDebug(
                "Cannot resolve OpenAPI tools: PluginRepository={HasRepo}, OpenApiToolFactory={HasFactory}",
                _pluginRepository != null,
                _openApiToolFactory != null);
            return null;
        }

        try
        {
            var plugin = await _pluginRepository.GetBySystemNameAsync(systemName, cancellationToken);

            if (plugin == null)
            {
                _logger.LogDebug("No plugin found with system name: '{SystemName}'", systemName);
                return null;
            }

            if (!plugin.IsActive)
            {
                _logger.LogDebug("Plugin '{SystemName}' is not active", systemName);
                return null;
            }

            if (!_openApiToolFactory.CanHandle(plugin))
            {
                _logger.LogDebug(
                    "Plugin '{SystemName}' (type: {PluginType}) cannot be handled by OpenApiToolFactory",
                    systemName,
                    plugin.PluginType);
                return null;
            }

            var tool = await _openApiToolFactory.CreateToolAsync(plugin, cancellationToken);

            if (tool != null)
            {
                _logger.LogDebug(
                    "Resolved OpenAPI tool '{SystemName}' → {ToolType}",
                    systemName,
                    tool.GetType().Name);
            }

            return tool;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve OpenAPI tool '{SystemName}'", systemName);
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AIFunction> GetAIFunctionsBySystemName(string systemName)
    {
        var tool = GetToolBySystemName(systemName);
        if (tool == null)
        {
            return Array.Empty<AIFunction>();
        }

        return tool.GetAIFunctions();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIFunction>> GetAIFunctionsBySystemNameAsync(
        string systemName,
        CancellationToken cancellationToken = default)
    {
        var tool = await GetToolBySystemNameAsync(systemName, cancellationToken);
        if (tool == null)
        {
            return Array.Empty<AIFunction>();
        }

        return tool.GetAIFunctions();
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ITool> GetAllTools()
    {
        // Return unique builtin tools (avoid duplicates from aliases)
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

    /// <inheritdoc />
    public bool IsToolRegistered(string systemName)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return false;
        }

        var normalizedName = systemName.Trim().ToLowerInvariant();
        return _toolFactories.ContainsKey(normalizedName);
    }

    /// <inheritdoc />
    public async Task<bool> IsToolRegisteredAsync(string systemName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(systemName))
        {
            return false;
        }

        // Check builtin first
        if (IsToolRegistered(systemName))
        {
            return true;
        }

        // Check OpenAPI tools from database
        if (_pluginRepository == null)
        {
            return false;
        }

        try
        {
            var plugin = await _pluginRepository.GetBySystemNameAsync(systemName, cancellationToken);
            return plugin != null && plugin.IsActive;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if tool '{SystemName}' is registered", systemName);
            return false;
        }
    }
}
