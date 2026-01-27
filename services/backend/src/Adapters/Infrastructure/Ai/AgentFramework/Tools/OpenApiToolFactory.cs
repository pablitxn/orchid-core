using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Infrastructure.Ai.AgentFramework.Tools;

/// <summary>
/// Factory for creating OpenAPI tools with spec fetching and caching capabilities.
/// Manages spec caching in the database with configurable TTL.
/// </summary>
public sealed class OpenApiToolFactory : IOpenApiToolFactory
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IActivityPublisher _activityPublisher;
    private readonly IPluginRepository _pluginRepository;
    private readonly ILogger<OpenApiToolFactory> _logger;
    private readonly ILogger<OpenApiTool> _toolLogger;
    private readonly TimeSpan _cacheTtl;
    private readonly TimeSpan _fetchTimeout;

    /// <summary>
    /// Default cache TTL of 1 hour.
    /// </summary>
    public static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// Default fetch timeout of 10 seconds.
    /// </summary>
    public static readonly TimeSpan DefaultFetchTimeout = TimeSpan.FromSeconds(10);

    public OpenApiToolFactory(
        IHttpClientFactory httpClientFactory,
        IActivityPublisher activityPublisher,
        IPluginRepository pluginRepository,
        ILogger<OpenApiToolFactory> logger,
        ILogger<OpenApiTool> toolLogger,
        TimeSpan? cacheTtl = null,
        TimeSpan? fetchTimeout = null)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _activityPublisher = activityPublisher ?? throw new ArgumentNullException(nameof(activityPublisher));
        _pluginRepository = pluginRepository ?? throw new ArgumentNullException(nameof(pluginRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _toolLogger = toolLogger ?? throw new ArgumentNullException(nameof(toolLogger));
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
        _fetchTimeout = fetchTimeout ?? DefaultFetchTimeout;
    }

    /// <inheritdoc />
    public bool CanHandle(PluginEntity plugin)
    {
        if (plugin == null) return false;

        return plugin.PluginType == "openapi" && !string.IsNullOrEmpty(plugin.BaseUrl);
    }

    /// <inheritdoc />
    public async Task<ITool?> CreateToolAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (!CanHandle(plugin))
        {
            _logger.LogWarning(
                "Cannot create OpenAPI tool for plugin {PluginName}: PluginType={PluginType}, BaseUrl={BaseUrl}",
                plugin.Name, plugin.PluginType, plugin.BaseUrl ?? "(null)");
            return null;
        }

        try
        {
            var spec = await GetOrFetchSpecAsync(plugin, forceRefresh: false, cancellationToken);
            if (spec == null)
            {
                return null;
            }

            var document = ParseSpec(spec, plugin);
            if (document == null)
            {
                return null;
            }

            return OpenApiTool.Create(
                plugin,
                document,
                _httpClientFactory,
                _activityPublisher,
                _toolLogger);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create OpenAPI tool for plugin {PluginName}", plugin.Name);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task RefreshSpecAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        if (!CanHandle(plugin))
        {
            _logger.LogWarning(
                "Cannot refresh spec for plugin {PluginName}: not an OpenAPI plugin",
                plugin.Name);
            return;
        }

        _logger.LogInformation("Force refreshing OpenAPI spec for plugin {PluginName}", plugin.Name);

        await GetOrFetchSpecAsync(plugin, forceRefresh: true, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIFunction>> CreateFunctionsAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
    {
        var tool = await CreateToolAsync(plugin, cancellationToken);
        return tool?.GetAIFunctions() ?? Array.Empty<AIFunction>();
    }

    private async Task<string?> GetOrFetchSpecAsync(
        PluginEntity plugin,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        // Check if we have a valid cached spec
        if (!forceRefresh && IsCacheValid(plugin))
        {
            _logger.LogDebug(
                "Using cached OpenAPI spec for plugin {PluginName}, cached at {CachedAt}",
                plugin.Name, plugin.SpecCachedAt);
            return plugin.OpenApiSpecCache;
        }

        // Need to fetch fresh spec
        var spec = await FetchSpecAsync(plugin, cancellationToken);
        if (spec == null)
        {
            // Fetch failed - use cached spec if available (even if expired)
            if (!string.IsNullOrEmpty(plugin.OpenApiSpecCache))
            {
                _logger.LogWarning(
                    "Using expired cached spec for plugin {PluginName} due to fetch failure",
                    plugin.Name);
                return plugin.OpenApiSpecCache;
            }

            return null;
        }

        // Update cache in database
        await UpdateCacheAsync(plugin, spec, cancellationToken);

        return spec;
    }

    private bool IsCacheValid(PluginEntity plugin)
    {
        if (string.IsNullOrEmpty(plugin.OpenApiSpecCache))
        {
            return false;
        }

        if (!plugin.SpecCachedAt.HasValue)
        {
            return false;
        }

        var cacheAge = DateTime.UtcNow - plugin.SpecCachedAt.Value;
        return cacheAge < _cacheTtl;
    }

    private async Task<string?> FetchSpecAsync(PluginEntity plugin, CancellationToken cancellationToken)
    {
        var specUrl = BuildSpecUrl(plugin);

        _logger.LogInformation(
            "Fetching OpenAPI spec for plugin {PluginName} from {SpecUrl}",
            plugin.Name, specUrl);

        try
        {
            var httpClient = _httpClientFactory.CreateClient(string.Empty);
            httpClient.Timeout = _fetchTimeout;

            var response = await httpClient.GetAsync(specUrl, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to fetch OpenAPI spec for plugin {PluginName}: HTTP {StatusCode}",
                    plugin.Name, (int)response.StatusCode);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogInformation(
                "Successfully fetched OpenAPI spec for plugin {PluginName} ({ContentLength} bytes)",
                plugin.Name, content.Length);

            return content;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Timeout fetching OpenAPI spec for plugin {PluginName} from {SpecUrl}",
                plugin.Name, specUrl);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "HTTP error fetching OpenAPI spec for plugin {PluginName}: {Message}",
                plugin.Name, ex.Message);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Unexpected error fetching OpenAPI spec for plugin {PluginName}",
                plugin.Name);
            return null;
        }
    }

    private static string BuildSpecUrl(PluginEntity plugin)
    {
        var baseUrl = plugin.BaseUrl!.TrimEnd('/');
        var specPath = plugin.OpenApiSpecUrl ?? "/openapi.json";

        if (!specPath.StartsWith('/'))
        {
            specPath = "/" + specPath;
        }

        return baseUrl + specPath;
    }

    private OpenApiDocument? ParseSpec(string specContent, PluginEntity plugin)
    {
        try
        {
            var reader = new OpenApiStringReader();
            var document = reader.Read(specContent, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                var errors = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
                _logger.LogError(
                    "OpenAPI spec parsing errors for plugin {PluginName}: {Errors}",
                    plugin.Name, errors);

                // Clear stale cache on parse failure
                _ = ClearCacheAsync(plugin, CancellationToken.None);

                return null;
            }

            if (diagnostic.Warnings.Count > 0)
            {
                var warnings = string.Join("; ", diagnostic.Warnings.Select(w => w.Message));
                _logger.LogWarning(
                    "OpenAPI spec warnings for plugin {PluginName}: {Warnings}",
                    plugin.Name, warnings);
            }

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to parse OpenAPI spec for plugin {PluginName}",
                plugin.Name);

            // Clear stale cache on parse failure
            _ = ClearCacheAsync(plugin, CancellationToken.None);

            return null;
        }
    }

    private async Task UpdateCacheAsync(PluginEntity plugin, string spec, CancellationToken cancellationToken)
    {
        try
        {
            plugin.OpenApiSpecCache = spec;
            plugin.SpecCachedAt = DateTime.UtcNow;

            await _pluginRepository.UpdateAsync(plugin, cancellationToken);

            _logger.LogDebug(
                "Updated OpenAPI spec cache for plugin {PluginName}",
                plugin.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist OpenAPI spec cache for plugin {PluginName}",
                plugin.Name);
        }
    }

    private async Task ClearCacheAsync(PluginEntity plugin, CancellationToken cancellationToken)
    {
        try
        {
            plugin.OpenApiSpecCache = null;
            plugin.SpecCachedAt = null;

            await _pluginRepository.UpdateAsync(plugin, cancellationToken);

            _logger.LogInformation(
                "Cleared OpenAPI spec cache for plugin {PluginName}",
                plugin.Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to clear OpenAPI spec cache for plugin {PluginName}",
                plugin.Name);
        }
    }
}
