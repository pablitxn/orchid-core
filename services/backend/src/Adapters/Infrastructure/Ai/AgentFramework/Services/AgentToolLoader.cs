using System.Diagnostics;
using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Middleware;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Service responsible for dynamically loading tools based on agent configuration.
/// Uses IToolRegistry for tool resolution instead of hardcoded switch statements.
/// All loaded functions are wrapped with error handling to prevent tool failures from crashing conversations.
/// Optionally wraps functions with telemetry tracking when IEnhancedTelemetryClient is provided.
/// </summary>
public class AgentToolLoader : IAgentToolLoader
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<AgentToolLoader> _logger;
    private readonly IEnhancedTelemetryClient? _telemetryClient;
    private readonly bool _enableSafeWrapping;
    private readonly bool _enableTelemetryWrapping;

    public AgentToolLoader(
        IPluginRepository pluginRepository,
        IToolRegistry toolRegistry,
        ILogger<AgentToolLoader> logger,
        IEnhancedTelemetryClient? telemetryClient = null,
        bool enableSafeWrapping = true,
        bool enableTelemetryWrapping = true)
    {
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _telemetryClient = telemetryClient;
        _enableSafeWrapping = enableSafeWrapping;
        _enableTelemetryWrapping = enableTelemetryWrapping && telemetryClient != null;
    }

    public async Task<IReadOnlyList<AIFunction>> LoadAgentToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var loadedTools = new List<AIFunction>();
        var loadedToolNames = new List<string>();

        try
        {
            var plugins = await _pluginRepository.ListByAgentAsync(agentId, cancellationToken);

            foreach (var plugin in plugins.Where(p => p.IsActive && !string.IsNullOrEmpty(p.SystemName)))
            {
                try
                {
                    var toolFunctions = _toolRegistry.GetAIFunctionsBySystemName(plugin.SystemName!);

                    if (toolFunctions.Count > 0)
                    {
                        loadedTools.AddRange(toolFunctions);
                        loadedToolNames.Add(plugin.SystemName!);
                        _logger.LogDebug(
                            "Loaded {FunctionCount} functions for tool '{SystemName}' (agent {AgentId})",
                            toolFunctions.Count,
                            plugin.SystemName,
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
                loadedTools.Count,
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

            // Apply wrappers in order: Telemetry -> Safe (so telemetry captures errors before they're converted)
            var wrappedTools = loadedTools.AsEnumerable();

            // First wrap with telemetry tracking (innermost wrapper)
            if (_enableTelemetryWrapping && _telemetryClient != null && loadedTools.Count > 0)
            {
                _logger.LogDebug(
                    "Wrapping {ToolCount} functions with telemetry tracking for agent {AgentId}",
                    loadedTools.Count,
                    agentId);
                wrappedTools = TelemetryAIFunctionWrapper.WrapAll(wrappedTools, _telemetryClient, _logger);
            }

            // Then wrap with safe error handling (outermost wrapper)
            if (_enableSafeWrapping && loadedTools.Count > 0)
            {
                _logger.LogDebug(
                    "Wrapping {ToolCount} functions with safe error handling for agent {AgentId}",
                    loadedTools.Count,
                    agentId);
                wrappedTools = SafeAIFunctionWrapper.WrapAll(wrappedTools, _logger);
            }

            return wrappedTools.ToList();
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
}
