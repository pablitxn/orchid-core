using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.AgentFramework.Tools;

/// <summary>
/// Factory for creating AI functions from OpenAPI plugin entities.
/// </summary>
public sealed class OpenApiToolFactory : IOpenApiToolFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IActivityPublisher _activityPublisher;
    private readonly ILogger<OpenApiTool> _toolLogger;

    public OpenApiToolFactory(
        IHttpClientFactory httpClientFactory,
        IActivityPublisher activityPublisher,
        ILogger<OpenApiTool> toolLogger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _activityPublisher = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
        _toolLogger = toolLogger ?? throw new ArgumentNullException(nameof(toolLogger));
    }

    public async Task<IReadOnlyList<AIFunction>> CreateFunctionsAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (plugin.PluginType != "openapi")
        {
            throw new ArgumentException($"Plugin '{plugin.Name}' is not an OpenAPI plugin (type: {plugin.PluginType})", nameof(plugin));
        }

        if (string.IsNullOrEmpty(plugin.BaseUrl))
        {
            throw new ArgumentException($"Plugin '{plugin.Name}' does not have a BaseUrl configured", nameof(plugin));
        }

        var tool = await OpenApiTool.CreateAsync(
            plugin,
            _httpClientFactory,
            _activityPublisher,
            _toolLogger,
            cancellationToken);

        return tool.GetAIFunctions();
    }
}
