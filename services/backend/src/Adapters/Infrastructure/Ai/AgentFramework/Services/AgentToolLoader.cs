using System.Diagnostics;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

// Note: IOpenApiToolFactory is in Infrastructure.Ai.AgentFramework.Abstractions

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Service responsible for dynamically loading tools based on agent configuration.
/// Uses IToolRegistry for tool resolution instead of hardcoded switch statements.
/// All loaded functions are wrapped with error handling to prevent tool failures from crashing conversations.
/// Optionally wraps functions with telemetry tracking when IEnhancedTelemetryClient is provided.
/// Optionally tracks credit consumption when ICreditTrackingService is provided.
/// </summary>
public class AgentToolLoader : IAgentToolLoader
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<AgentToolLoader> _logger;
    private readonly IEnhancedTelemetryClient? _telemetryClient;
    private readonly ICreditTrackingService? _creditTrackingService;
    private readonly IOpenApiToolFactory? _openApiToolFactory;
    private readonly bool _enableSafeWrapping;
    private readonly bool _enableTelemetryWrapping;
    private readonly bool _enableCreditTracking;

    public AgentToolLoader(
        IPluginRepository pluginRepository,
        IToolRegistry toolRegistry,
        ILogger<AgentToolLoader> logger,
        IEnhancedTelemetryClient? telemetryClient = null,
        ICreditTrackingService? creditTrackingService = null,
        IOpenApiToolFactory? openApiToolFactory = null,
        bool enableSafeWrapping = true,
        bool enableTelemetryWrapping = true,
        bool enableCreditTracking = true)
    {
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryClient = telemetryClient;
        _creditTrackingService = creditTrackingService;
        _openApiToolFactory = openApiToolFactory;
        _enableSafeWrapping = enableSafeWrapping;
        _enableTelemetryWrapping = enableTelemetryWrapping && telemetryClient != null;
        _enableCreditTracking = enableCreditTracking && creditTrackingService != null;
    }

    public async Task<IReadOnlyList<AIFunction>> LoadAgentToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var allWrappedFunctions = new List<AIFunction>();
        var loadedToolNames = new List<string>();
        var totalFunctionCount = 0;

        try
        {
            var plugins = await _pluginRepository.ListByAgentAsync(agentId, cancellationToken);

            foreach (var plugin in plugins.Where(p => p.IsActive))
            {
                try
                {
                    IReadOnlyList<AIFunction> toolFunctions;
                    string toolSystemName;

                    // Handle OpenAPI plugins dynamically
                    if (plugin.PluginType == "openapi")
                    {
                        var openApiFunctions = await LoadOpenApiToolAsync(plugin, cancellationToken);
                        if (openApiFunctions == null || openApiFunctions.Count == 0)
                        {
                            continue;
                        }
                        toolFunctions = openApiFunctions;
                        toolSystemName = plugin.SystemName ?? plugin.Name.ToLowerInvariant().Replace(" ", "_");
                    }
                    else
                    {
                        // Handle builtin plugins via registry
                        if (string.IsNullOrEmpty(plugin.SystemName))
                        {
                            continue;
                        }
                        toolFunctions = _toolRegistry.GetAIFunctionsBySystemName(plugin.SystemName);
                        toolSystemName = plugin.SystemName;
                    }

                    if (toolFunctions.Count > 0)
                    {
                        // Apply wrappers per-tool so each function knows its tool system name
                        var wrappedFunctions = WrapToolFunctions(toolFunctions, toolSystemName);
                        allWrappedFunctions.AddRange(wrappedFunctions);
                        loadedToolNames.Add(toolSystemName);
                        totalFunctionCount += toolFunctions.Count;

                        _logger.LogDebug(
                            "Loaded {FunctionCount} functions for tool '{SystemName}' (agent {AgentId})",
                            toolFunctions.Count,
                            toolSystemName,
                            agentId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Tool '{SystemName}' not found in registry for plugin {PluginId}",
                            plugin.SystemName,
                            plugin.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to load tool {PluginName} ({PluginId}) for agent {AgentId}",
                        plugin.Name,
                        plugin.Id,
                        agentId);
                }
            }

            stopwatch.Stop();

            _logger.LogInformation(
                "Loaded {ToolCount} tool sets ({FunctionCount} functions) for agent {AgentId} in {ElapsedMs}ms: [{Tools}]",
                loadedToolNames.Count,
                totalFunctionCount,
                agentId,
                stopwatch.ElapsedMilliseconds,
                string.Join(", ", loadedToolNames));

            if (stopwatch.ElapsedMilliseconds > 50)
            {
                _logger.LogWarning(
                    "Tool loading exceeded performance target (50ms) for agent {AgentId}: {ElapsedMs}ms",
                    agentId,
                    stopwatch.ElapsedMilliseconds);
            }

            return allWrappedFunctions;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(
                ex,
                "Failed to load tools for agent {AgentId} after {ElapsedMs}ms",
                agentId,
                stopwatch.ElapsedMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Wraps tool functions with telemetry and safe error handling.
    /// Order: innermost (telemetry with credit tracking) -> outermost (safe error handling).
    /// </summary>
    private IEnumerable<AIFunction> WrapToolFunctions(IReadOnlyList<AIFunction> functions, string toolSystemName)
    {
        IEnumerable<AIFunction> wrappedFunctions = functions;

        // First wrap with telemetry tracking (innermost wrapper) - includes credit tracking
        if (_enableTelemetryWrapping && _telemetryClient != null)
        {
            if (_enableCreditTracking && _creditTrackingService != null)
            {
                // Wrap with telemetry AND credit tracking
                wrappedFunctions = TelemetryAIFunctionWrapper.WrapAllWithCreditTracking(
                    wrappedFunctions,
                    _telemetryClient,
                    _logger,
                    _creditTrackingService,
                    _pluginRepository,
                    toolSystemName);
            }
            else
            {
                // Wrap with telemetry only
                wrappedFunctions = TelemetryAIFunctionWrapper.WrapAll(wrappedFunctions, _telemetryClient, _logger);
            }
        }

        // Then wrap with safe error handling (outermost wrapper)
        if (_enableSafeWrapping)
        {
            wrappedFunctions = SafeAIFunctionWrapper.WrapAll(wrappedFunctions, _logger);
        }

        return wrappedFunctions;
    }

    /// <summary>
    /// Loads an OpenAPI tool dynamically from a plugin entity.
    /// </summary>
    private async Task<IReadOnlyList<AIFunction>?> LoadOpenApiToolAsync(
        PluginEntity plugin,
        CancellationToken cancellationToken)
    {
        if (_openApiToolFactory == null)
        {
            _logger.LogWarning(
                "OpenAPI tool factory not configured, skipping plugin {PluginName} ({PluginId})",
                plugin.Name,
                plugin.Id);
            return null;
        }

        if (string.IsNullOrEmpty(plugin.BaseUrl))
        {
            _logger.LogWarning(
                "OpenAPI plugin {PluginName} ({PluginId}) has no BaseUrl configured",
                plugin.Name,
                plugin.Id);
            return null;
        }

        try
        {
            var functions = await _openApiToolFactory.CreateFunctionsAsync(plugin, cancellationToken);

            _logger.LogInformation(
                "Created OpenAPI tool '{PluginName}' with {FunctionCount} functions from spec",
                plugin.Name,
                functions.Count);

            return functions;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to create OpenAPI tool from plugin {PluginName} ({PluginId})",
                plugin.Name,
                plugin.Id);
            return null;
        }
    }
}
