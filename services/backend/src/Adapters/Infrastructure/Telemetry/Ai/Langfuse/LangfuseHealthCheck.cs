using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Telemetry.Ai.Langfuse;

public class LangfuseHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<LangfuseSettings> _settings;
    private readonly ILogger<LangfuseHealthCheck> _logger;

    public LangfuseHealthCheck(
        HttpClient httpClient,
        IOptions<LangfuseSettings> settings,
        ILogger<LangfuseHealthCheck> logger)
    {
        _httpClient = httpClient;
        _settings = settings;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = _settings.Value;
            
            // Check if Langfuse is configured
            if (string.IsNullOrEmpty(settings.PublicKey) || 
                string.IsNullOrEmpty(settings.SecretKey) ||
                string.IsNullOrEmpty(settings.BaseUrl))
            {
                return HealthCheckResult.Unhealthy(
                    "Langfuse telemetry is not configured. Missing required settings.");
            }

            // Try to ping the Langfuse health endpoint
            // According to Langfuse API docs, the health endpoint is at /api/public/health
            var baseUrl = settings.BaseUrl.TrimEnd('/');
            var healthUrl = baseUrl.EndsWith("/api/public") 
                ? $"{baseUrl}/health" 
                : $"{baseUrl}/api/public/health";
            var response = await _httpClient.GetAsync(healthUrl, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var data = new Dictionary<string, object>
                {
                    ["baseUrl"] = settings.BaseUrl,
                    ["configured"] = true,
                    ["status"] = "connected"
                };

                return HealthCheckResult.Healthy(
                    "Langfuse telemetry service is healthy and configured.", 
                    data);
            }

            return HealthCheckResult.Unhealthy(
                $"Langfuse health check failed with status {response.StatusCode}",
                null,
                new Dictionary<string, object>
                {
                    ["statusCode"] = (int)response.StatusCode,
                    ["reason"] = response.ReasonPhrase ?? "Unknown"
                });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to Langfuse service");
            return HealthCheckResult.Unhealthy(
                "Cannot connect to Langfuse service",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["error"] = ex.Message
                });
        }
        catch (TaskCanceledException)
        {
            return HealthCheckResult.Unhealthy(
                "Langfuse health check timed out",
                data: new Dictionary<string, object>
                {
                    ["error"] = "Request timeout"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in Langfuse health check");
            return HealthCheckResult.Unhealthy(
                "Unexpected error checking Langfuse health",
                exception: ex);
        }
    }
}