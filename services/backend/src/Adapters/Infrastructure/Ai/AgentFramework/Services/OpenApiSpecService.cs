using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Readers;

namespace Infrastructure.Ai.AgentFramework.Services;

/// <summary>
/// Adapter implementing IOpenApiSpecService using IOpenApiToolFactory.
/// </summary>
public sealed class OpenApiSpecService : IOpenApiSpecService
{
    private readonly IOpenApiToolFactory _toolFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OpenApiSpecService> _logger;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(10);

    public OpenApiSpecService(
        IOpenApiToolFactory toolFactory,
        IHttpClientFactory httpClientFactory,
        ILogger<OpenApiSpecService> logger)
    {
        _toolFactory = toolFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OpenApiSpecValidationResult> ValidateSpecAsync(
        string baseUrl,
        string? specPath,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var operations = new List<OpenApiFunction>();

        try
        {
            var trimmedBaseUrl = baseUrl.TrimEnd('/');
            var normalizedSpecPath = specPath ?? "/openapi.json";
            if (!normalizedSpecPath.StartsWith('/'))
                normalizedSpecPath = "/" + normalizedSpecPath;
            var specUrl = trimmedBaseUrl + normalizedSpecPath;

            _logger.LogInformation("Validating OpenAPI spec at {SpecUrl}", specUrl);

            var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = FetchTimeout;

            try
            {
                var baseResponse = await httpClient.GetAsync(trimmedBaseUrl, cancellationToken);
                if (!baseResponse.IsSuccessStatusCode)
                {
                    warnings.Add($"Base URL returned status {(int)baseResponse.StatusCode}. The service may not be fully available.");
                }
            }
            catch (HttpRequestException ex)
            {
                warnings.Add($"Could not reach base URL: {ex.Message}. Continuing to check spec URL.");
            }

            var specResponse = await httpClient.GetAsync(specUrl, cancellationToken);

            if (!specResponse.IsSuccessStatusCode)
            {
                errors.Add($"Failed to fetch OpenAPI spec: HTTP {(int)specResponse.StatusCode}");
                return new OpenApiSpecValidationResult(
                    IsValid: false,
                    Title: null,
                    Version: null,
                    Operations: operations,
                    Errors: errors,
                    Warnings: warnings);
            }

            var specContent = await specResponse.Content.ReadAsStringAsync(cancellationToken);

            var reader = new OpenApiStringReader();
            var document = reader.Read(specContent, out var diagnostic);

            if (diagnostic.Errors.Count > 0)
            {
                errors.AddRange(diagnostic.Errors.Select(e => $"Parse error: {e.Message}"));
            }

            if (diagnostic.Warnings.Count > 0)
            {
                warnings.AddRange(diagnostic.Warnings.Select(w => $"Parse warning: {w.Message}"));
            }

            if (document is not null)
            {
                foreach (var pathItem in document.Paths)
                {
                    foreach (var operation in pathItem.Value.Operations)
                    {
                        var functionName = !string.IsNullOrEmpty(operation.Value.OperationId)
                            ? operation.Value.OperationId
                            : $"{operation.Key}{pathItem.Key.Replace("/", "_").Replace("{", "").Replace("}", "")}";

                        operations.Add(new OpenApiFunction(
                            functionName,
                            operation.Value.Summary ?? operation.Value.Description,
                            operation.Key.ToString().ToUpperInvariant(),
                            pathItem.Key));
                    }
                }
            }

            var isValid = errors.Count == 0 && document is not null;

            _logger.LogInformation(
                "OpenAPI spec validation completed: IsValid={IsValid}, Operations={OperationCount}, Errors={ErrorCount}",
                isValid,
                operations.Count,
                errors.Count);

            return new OpenApiSpecValidationResult(
                IsValid: isValid,
                Title: document?.Info?.Title,
                Version: document?.Info?.Version,
                Operations: operations,
                Errors: errors,
                Warnings: warnings);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            errors.Add("Request timed out while fetching the OpenAPI spec.");
            return new OpenApiSpecValidationResult(
                IsValid: false,
                Title: null,
                Version: null,
                Operations: operations,
                Errors: errors,
                Warnings: warnings);
        }
        catch (HttpRequestException ex)
        {
            errors.Add($"HTTP error: {ex.Message}");
            return new OpenApiSpecValidationResult(
                IsValid: false,
                Title: null,
                Version: null,
                Operations: operations,
                Errors: errors,
                Warnings: warnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating OpenAPI spec");
            errors.Add($"Unexpected error: {ex.Message}");
            return new OpenApiSpecValidationResult(
                IsValid: false,
                Title: null,
                Version: null,
                Operations: operations,
                Errors: errors,
                Warnings: warnings);
        }
    }

    public async Task RefreshSpecAsync(PluginEntity plugin, CancellationToken cancellationToken = default)
    {
        await _toolFactory.RefreshSpecAsync(plugin, cancellationToken);
    }

    public async Task<IReadOnlyList<OpenApiFunction>> GetFunctionsAsync(
        PluginEntity plugin,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var aiFunctions = await _toolFactory.CreateFunctionsAsync(plugin, cancellationToken);

            return aiFunctions.Select(f => new OpenApiFunction(
                f.Name,
                f.Description,
                ExtractHttpMethod(f.Name),
                "/"
            )).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get functions for plugin {PluginId}", plugin.Id);
            return [];
        }
    }

    private static string ExtractHttpMethod(string functionName)
    {
        var methods = new[] { "Get", "Post", "Put", "Delete", "Patch", "Head", "Options" };
        foreach (var method in methods)
        {
            if (functionName.StartsWith(method, StringComparison.OrdinalIgnoreCase))
                return method.ToUpperInvariant();
        }
        return "GET";
    }
}
