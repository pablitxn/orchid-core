using Application.Interfaces;
using Infrastructure.Ai.SemanticKernel.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Infrastructure.Ai.SemanticKernel.Services;

/// <summary>
/// Service responsible for dynamically loading plugins based on agent configuration
/// </summary>
public class AgentPluginLoader(
    IPluginRepository pluginRepository,
    IServiceProvider serviceProvider,
    ILogger<AgentPluginLoader> logger)
    : IAgentPluginLoader
{
    public async Task<IReadOnlyList<string>> LoadAgentPluginsAsync(
        Guid agentId,
        Kernel kernel,
        CancellationToken cancellationToken = default)
    {
        var loadedPlugins = new List<string>();

        try
        {
            // Get plugins associated with the agent
            var plugins = await pluginRepository.ListByAgentAsync(agentId, cancellationToken);

            foreach (var plugin in plugins.Where(p => p.IsActive && !string.IsNullOrEmpty(p.SystemName)))
            {
                try
                {
                    // Load plugin based on its system name
                    switch (plugin.SystemName?.ToLowerInvariant())
                    {
                        case "excel":
                        case "spreadsheet":
                            var spreadsheetPlugin = serviceProvider.GetRequiredService<SpreadsheetPluginV3Refactored>();
                            kernel.ImportPluginFromObject(spreadsheetPlugin, "excel");
                            loadedPlugins.Add("excel");
                            logger.LogInformation("Loaded Excel/Spreadsheet plugin for agent {AgentId}", agentId);
                            break;

                        case "vector_store":
                        case "vectorstore":
                            var vectorStorePlugin = serviceProvider.GetRequiredService<VectorStorePlugin>();
                            kernel.ImportPluginFromObject(vectorStorePlugin, "vector_store");
                            loadedPlugins.Add("vector_store");
                            logger.LogInformation("Loaded Vector Store plugin for agent {AgentId}", agentId);
                            break;

                        case "math":
                        case "mathengine":
                            var mathPlugin = serviceProvider.GetRequiredService<MathEnginePlugin>();
                            kernel.ImportPluginFromObject(mathPlugin, "math");
                            loadedPlugins.Add("math");
                            logger.LogInformation("Loaded Math Engine plugin for agent {AgentId}", agentId);
                            break;

                        case "web_search":
                        case "websearch":
                            var webSearchPlugin = serviceProvider.GetRequiredService<WebSearchPlugin>();
                            kernel.ImportPluginFromObject(webSearchPlugin, "web_search");
                            loadedPlugins.Add("web_search");
                            logger.LogInformation("Loaded Web Search plugin for agent {AgentId}", agentId);
                            break;

                        default:
                            logger.LogWarning(
                                "Unknown plugin system name '{SystemName}' for plugin {PluginId}",
                                plugin.SystemName,
                                plugin.Id);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(
                        ex,
                        "Failed to load plugin {PluginName} ({PluginId}) for agent {AgentId}",
                        plugin.Name,
                        plugin.Id,
                        agentId);
                }
            }

            logger.LogInformation(
                "Loaded {Count} plugins for agent {AgentId}: {Plugins}",
                loadedPlugins.Count,
                agentId,
                string.Join(", ", loadedPlugins));

            return loadedPlugins;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load plugins for agent {AgentId}", agentId);
            throw;
        }
    }
}