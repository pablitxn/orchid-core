using System.Reflection;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.SemanticKernel;

namespace Infrastructure.Ai.SemanticKernel.Services;

public class PluginDiscoveryService : IPluginDiscoveryService
{
    private readonly Dictionary<string, (string name, string description, int defaultPrice, bool isSubscription)>
        _pluginMetadata = new()
        {
            ["vector_store"] = ("Vector Store", "Store and search documents using semantic similarity", 15, false),
            ["web_search"] = ("Web Search", "Search the web and fetch content from URLs", 25, true)
        };

    public Task<IEnumerable<DiscoveredPlugin>> DiscoverPluginsAsync(CancellationToken cancellationToken = default)
    {
        var discoveredPlugins = new List<DiscoveredPlugin>();

        // Get the plugins assembly
        var pluginsAssembly = Assembly.GetAssembly(typeof(Plugins.VectorStorePlugin));
        if (pluginsAssembly == null) return Task.FromResult(discoveredPlugins.AsEnumerable());

        // Find all plugin types
        var pluginTypes = pluginsAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.Namespace?.Contains("Plugins") == true)
            .ToList();

        foreach (var pluginType in pluginTypes)
        {
            // Find all methods with KernelFunction attribute
            var kernelFunctions = pluginType.GetMethods()
                .Where(m => m.GetCustomAttribute<KernelFunctionAttribute>() != null)
                .Select(m => m.GetCustomAttribute<KernelFunctionAttribute>()!.Name ?? m.Name)
                .ToList();

            if (kernelFunctions.Any())
            {
                var systemName = GetSystemNameForPlugin(pluginType.Name);
                if (systemName != null && _pluginMetadata.TryGetValue(systemName, out var metadata))
                {
                    discoveredPlugins.Add(new DiscoveredPlugin(
                        Name: metadata.name,
                        SystemName: systemName,
                        Description: metadata.description,
                        Functions: kernelFunctions,
                        DefaultPriceCredits: metadata.defaultPrice,
                        IsSubscriptionBased: metadata.isSubscription
                    ));
                }
            }
        }

        return Task.FromResult(discoveredPlugins.AsEnumerable());
    }

    private string? GetSystemNameForPlugin(string pluginTypeName)
    {
        return pluginTypeName switch
        {
            "VectorStorePlugin" => "vector_store",
            "WebSearchPlugin" => "web_search",
            _ => null
        };
    }
}