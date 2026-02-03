using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace Infrastructure.Ai.AgentFramework.Tools;

/// <summary>
/// A dynamic tool that exposes HTTP operations from an OpenAPI specification as AI functions.
/// Each operation in the spec becomes an AIFunction that the AI agent can invoke.
/// </summary>
public sealed class OpenApiTool : ITool
{
    private readonly IActivityPublisher _activity;
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenApiTool> _logger;
    private readonly PluginEntity _plugin;
    private readonly OpenApiDocument _openApiDocument;
    private readonly List<AIFunction> _aiFunctions;

    public string SystemName => _plugin.SystemName ?? _plugin.Name.ToLowerInvariant().Replace(" ", "_");
    public string DisplayName => _plugin.Name;
    public string Description => _plugin.Description ?? $"OpenAPI plugin: {_plugin.Name}";
    public int CreditCost => _plugin.PriceCredits;
    public bool RequiresSubscription => _plugin.IsSubscriptionBased;

    private OpenApiTool(
        PluginEntity plugin,
        OpenApiDocument openApiDocument,
        HttpClient httpClient,
        IActivityPublisher activity,
        ILogger<OpenApiTool> logger)
    {
        _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
        _openApiDocument = openApiDocument ?? throw new ArgumentNullException(nameof(openApiDocument));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _activity = activity ?? throw new ArgumentNullException(nameof(activity));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _aiFunctions = BuildAIFunctions();
    }

    /// <summary>
    /// Creates an OpenApiTool instance from an already parsed OpenAPI document.
    /// This is the preferred factory method when the spec is managed by OpenApiToolFactory.
    /// </summary>
    public static OpenApiTool Create(
        PluginEntity plugin,
        OpenApiDocument openApiDocument,
        IHttpClientFactory httpClientFactory,
        IActivityPublisher activity,
        ILogger<OpenApiTool> logger)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(openApiDocument);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = httpClientFactory.CreateClient(string.Empty);
        httpClient.Timeout = TimeSpan.FromSeconds(plugin.TimeoutSeconds ?? 30);

        if (!string.IsNullOrEmpty(plugin.BaseUrl))
        {
            httpClient.BaseAddress = new Uri(plugin.BaseUrl);
        }

        return new OpenApiTool(plugin, openApiDocument, httpClient, activity, logger);
    }

    /// <summary>
    /// Creates an OpenApiTool instance asynchronously by fetching and parsing the OpenAPI spec.
    /// Consider using OpenApiToolFactory.CreateToolAsync for proper caching support.
    /// </summary>
    public static async Task<OpenApiTool> CreateAsync(
        PluginEntity plugin,
        IHttpClientFactory httpClientFactory,
        IActivityPublisher activity,
        ILogger<OpenApiTool> logger,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        var httpClient = httpClientFactory.CreateClient(string.Empty);
        httpClient.Timeout = TimeSpan.FromSeconds(plugin.TimeoutSeconds ?? 30);

        if (!string.IsNullOrEmpty(plugin.BaseUrl))
        {
            httpClient.BaseAddress = new Uri(plugin.BaseUrl);
        }

        var openApiDoc = await FetchAndParseSpecAsync(plugin, httpClient, cancellationToken);

        return new OpenApiTool(plugin, openApiDoc, httpClient, activity, logger);
    }

    private static async Task<OpenApiDocument> FetchAndParseSpecAsync(
        PluginEntity plugin,
        HttpClient httpClient,
        CancellationToken cancellationToken)
    {
        string specContent;
        // Use cached spec if available and not too old (1 hour)
        if (!string.IsNullOrEmpty(plugin.OpenApiSpecCache) &&
            plugin.SpecCachedAt.HasValue &&
            DateTime.UtcNow - plugin.SpecCachedAt.Value < TimeSpan.FromHours(1))
        {
            specContent = plugin.OpenApiSpecCache;
        }
        else
        {
            // Fetch from URL
            var specUrl = plugin.OpenApiSpecUrl ?? "/openapi.json";
            var response = await httpClient.GetAsync(specUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            specContent = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        var reader = new OpenApiStringReader();
        var document = reader.Read(specContent, out var diagnostic);

        if (diagnostic.Errors.Count > 0)
        {
            var errors = string.Join("; ", diagnostic.Errors.Select(e => e.Message));
            throw new InvalidOperationException($"OpenAPI spec parsing errors: {errors}");
        }

        return document;
    }

    public IReadOnlyList<AIFunction> GetAIFunctions() => _aiFunctions.AsReadOnly();

    private List<AIFunction> BuildAIFunctions()
    {
        var functions = new List<AIFunction>();

        foreach (var pathItem in _openApiDocument.Paths)
        {
            var path = pathItem.Key;
            foreach (var operation in pathItem.Value.Operations)
            {
                var method = operation.Key;
                var op = operation.Value;

                var functionName = GenerateFunctionName(op.OperationId, path, method);
                var description = op.Summary ?? op.Description ?? $"{method} {path}";

                var function = CreateOperationFunction(path, method, op, functionName, description);
                functions.Add(function);
            }
        }

        return functions;
    }

    private static string GenerateFunctionName(string? operationId, string path, OperationType method)
    {
        if (!string.IsNullOrEmpty(operationId))
        {
            return SanitizeFunctionName(operationId);
        }

        // Generate from path and method: GET /users/{id} -> GetUsersById
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var nameParts = parts
            .Select(p => p.StartsWith('{') ? "By" + ToPascalCase(p.Trim('{', '}')) : ToPascalCase(p));

        return method.ToString() + string.Concat(nameParts);
    }

    private static string SanitizeFunctionName(string name)
    {
        // Remove non-alphanumeric characters and ensure valid function name
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.Length > 0 ? sb.ToString() : "UnnamedOperation";
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return char.ToUpperInvariant(input[0]) + input[1..].ToLowerInvariant();
    }

    private AIFunction CreateOperationFunction(
        string path,
        OperationType method,
        OpenApiOperation operation,
        string functionName,
        string description)
    {
        // Build parameter metadata for the AI
        var parameterDescriptions = new List<string>();
        foreach (var param in operation.Parameters)
        {
            var required = param.Required ? " (required)" : " (optional)";
            parameterDescriptions.Add($"- {param.Name}: {param.Description ?? param.Schema?.Type ?? "string"}{required}");
        }

        if (operation.RequestBody != null)
        {
            parameterDescriptions.Add("- requestBody: JSON object for the request body (required)");
        }

        var fullDescription = description;
        if (parameterDescriptions.Count > 0)
        {
            fullDescription += "\nParameters:\n" + string.Join("\n", parameterDescriptions);
        }

        // Create function that accepts a JSON object with all parameters
        return AIFunctionFactory.Create(
            async (JsonNode? parameters, CancellationToken ct) =>
                await ExecuteOperationAsync(path, method, operation, parameters, ct),
            functionName,
            fullDescription);
    }

    private async Task<string> ExecuteOperationAsync(
        string pathTemplate,
        OperationType method,
        OpenApiOperation operation,
        JsonNode? parameters,
        CancellationToken cancellationToken)
    {
        var paramsObj = parameters?.AsObject() ?? new JsonObject();

        _logger.LogInformation(
            "Executing OpenAPI operation {Method} {Path} for plugin {PluginName}",
            method, pathTemplate, _plugin.Name);

        try
        {
            // Build the URL with path parameters
            var url = BuildUrl(pathTemplate, operation.Parameters, paramsObj);

            // Create the HTTP request
            var httpMethod = method switch
            {
                OperationType.Get => HttpMethod.Get,
                OperationType.Post => HttpMethod.Post,
                OperationType.Put => HttpMethod.Put,
                OperationType.Delete => HttpMethod.Delete,
                OperationType.Patch => HttpMethod.Patch,
                OperationType.Head => HttpMethod.Head,
                OperationType.Options => HttpMethod.Options,
                _ => HttpMethod.Get
            };

            using var request = new HttpRequestMessage(httpMethod, url);

            // Add custom headers if provided
            if (paramsObj.TryGetPropertyValue("headers", out var headersNode)
                && headersNode is JsonObject headersObj)
            {
                foreach (var header in headersObj)
                {
                    var value = header.Value?.ToString();
                    if (!string.IsNullOrWhiteSpace(header.Key) &&
                        !string.IsNullOrWhiteSpace(value))
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, value);
                    }
                }
            }

            // Add header parameters defined in the OpenAPI spec
            foreach (var paramDef in operation.Parameters)
            {
                if (paramDef.In != ParameterLocation.Header)
                {
                    continue;
                }

                var headerName = paramDef.Name;
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    continue;
                }

                if (!paramsObj.TryGetPropertyValue(headerName, out var valueNode) ||
                    valueNode == null)
                {
                    continue;
                }

                var value = valueNode.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    request.Headers.TryAddWithoutValidation(headerName, value);
                }
            }

            // Add request body if present
            if (operation.RequestBody != null && paramsObj.ContainsKey("requestBody"))
            {
                var bodyNode = paramsObj["requestBody"];
                var bodyJson = bodyNode?.ToJsonString() ?? "{}";
                request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            }

            // Execute the request
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

            var result = new
            {
                statusCode = (int)response.StatusCode,
                success = response.IsSuccessStatusCode,
                data = TryParseJson(responseContent)
            };

            var resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

            // Publish activity for frontend
            await _activity.PublishAsync("tool_invocation", new
            {
                tool = SystemName,
                operation = $"{method} {pathTemplate}",
                parameters = paramsObj,
                result = resultJson
            }, cancellationToken);

            _logger.LogInformation(
                "OpenAPI operation {Method} {Path} completed with status {StatusCode}",
                method, pathTemplate, (int)response.StatusCode);

            return resultJson;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for {Method} {Path}", method, pathTemplate);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"HTTP request failed: {ex.Message}"
            });
        }
        catch (TaskCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Request cancelled for {Method} {Path}", method, pathTemplate);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Request was cancelled"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing {Method} {Path}", method, pathTemplate);
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Unexpected error: {ex.Message}"
            });
        }
    }

    private string BuildUrl(
        string pathTemplate,
        IList<OpenApiParameter> parameterDefs,
        JsonObject parameters)
    {
        var path = pathTemplate;
        var queryParams = new List<string>();

        foreach (var paramDef in parameterDefs)
        {
            var paramName = paramDef.Name;
            if (!parameters.TryGetPropertyValue(paramName, out var valueNode) || valueNode == null)
            {
                continue;
            }

            var value = valueNode.ToString();

            switch (paramDef.In)
            {
                case ParameterLocation.Path:
                    path = path.Replace($"{{{paramName}}}", Uri.EscapeDataString(value));
                    break;
                case ParameterLocation.Query:
                    queryParams.Add($"{Uri.EscapeDataString(paramName)}={Uri.EscapeDataString(value)}");
                    break;
                case ParameterLocation.Header:
                    // Headers would be added to the request, but we skip for simplicity
                    break;
            }
        }

        if (queryParams.Count > 0)
        {
            path += "?" + string.Join("&", queryParams);
        }

        return path;
    }

    private static object? TryParseJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch
        {
            // Return as plain string if not valid JSON
            return content;
        }
    }
}
