using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Service responsible for dynamically loading tools based on agent configuration.
/// Uses the ToolRegistry for centralized tool resolution.
/// </summary>
public class AgentToolLoader : IAgentToolLoader
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IToolRegistry _toolRegistry;
    private readonly ILogger<AgentToolLoader> _logger;

    public AgentToolLoader(
        IPluginRepository pluginRepository,
        IToolRegistry toolRegistry,
        ILogger<AgentToolLoader> logger)
    {
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<AIFunction>> LoadAgentToolsAsync(
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var loadedTools = new List<AIFunction>();
        var loadedToolNames = new List<string>();

        try
        {
            var plugins = await _pluginRepository.ListByAgentAsync(agentId, cancellationToken);

            foreach (var plugin in plugins.Where(p => p.IsActive && !string.IsNullOrEmpty(p.SystemName)))
            {
                try
                {
                    var tool = _toolRegistry.GetToolBySystemName(plugin.SystemName!);

                    if (tool != null)
                    {
                        loadedTools.AddRange(tool.GetAIFunctions());
                        loadedToolNames.Add(tool.SystemName);
                        _logger.LogInformation(
                            "Loaded {ToolName} tools for agent {AgentId}",
                            tool.DisplayName,
                            agentId);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Unknown plugin system name '{SystemName}' for plugin {PluginId}",
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

            _logger.LogInformation(
                "Loaded {Count} tool sets ({ToolCount} functions) for agent {AgentId}: {Tools}",
                loadedToolNames.Count,
                loadedTools.Count,
                agentId,
                string.Join(", ", loadedToolNames));

            return loadedTools;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tools for agent {AgentId}", agentId);
            throw;
        }
    }
}
