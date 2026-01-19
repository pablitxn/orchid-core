using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Service responsible for dynamically loading tools based on agent configuration.
/// </summary>
public class AgentToolLoader : IAgentToolLoader
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentToolLoader> _logger;

    public AgentToolLoader(
        IPluginRepository pluginRepository,
        IServiceProvider serviceProvider,
        ILogger<AgentToolLoader> logger)
    {
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
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
                    switch (plugin.SystemName?.ToLowerInvariant())
                    {
                        case "vector_store":
                        case "vectorstore":
                            var vectorStoreTool = _serviceProvider.GetRequiredService<VectorStoreTool>();
                            loadedTools.AddRange(vectorStoreTool.GetAIFunctions());
                            loadedToolNames.Add("vector_store");
                            _logger.LogInformation("Loaded Vector Store tools for agent {AgentId}", agentId);
                            break;

                        case "web_search":
                        case "websearch":
                            var webSearchTool = _serviceProvider.GetRequiredService<WebSearchTool>();
                            loadedTools.AddRange(webSearchTool.GetAIFunctions());
                            loadedToolNames.Add("web_search");
                            _logger.LogInformation("Loaded Web Search tools for agent {AgentId}", agentId);
                            break;

                        default:
                            _logger.LogWarning(
                                "Unknown plugin system name '{SystemName}' for plugin {PluginId}",
                                plugin.SystemName,
                                plugin.Id);
                            break;
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
