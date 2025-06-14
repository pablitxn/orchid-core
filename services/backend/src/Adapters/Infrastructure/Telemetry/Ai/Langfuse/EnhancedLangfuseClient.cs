using System.Net;
using System.Net.Http.Json;
using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Telemetry.Ai.Langfuse;

/// <summary>
/// Enhanced Langfuse client with session support and AI-specific telemetry features
/// </summary>
public class EnhancedLangfuseClient(HttpClient http, ILogger<EnhancedLangfuseClient> logger) : IEnhancedTelemetryClient
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    private readonly ILogger<EnhancedLangfuseClient>
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly Dictionary<string, DateTime> _traceStartTimes = new();

    public async Task<string> StartTraceAsync(string name, string? sessionId = null, string? userId = null,
        object? metadata = null, CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        // Store start time for latency calculation
        _traceStartTimes[traceId] = timestamp.UtcDateTime;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "trace-create",
                    body = new
                    {
                        id = traceId,
                        timestamp = timestamp.ToString("O"),
                        name = name,
                        sessionId = sessionId,
                        userId = userId,
                        metadata = metadata,
                        environment = "production",
                        tags = new[] { "ai", "chat" }
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to start trace. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting trace");
        }

        return traceId;
    }

    public Task<string> StartTraceAsync(string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return StartTraceAsync(name, null, null, metadata, cancellationToken);
    }

    public async Task EndTraceAsync(string traceId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        // Calculate latency if we have start time
        double? latencySeconds = null;
        if (_traceStartTimes.TryGetValue(traceId, out var startTime))
        {
            latencySeconds = (timestamp.UtcDateTime - startTime).TotalSeconds;
            _traceStartTimes.Remove(traceId);
        }

        var updateMetadata = metadata as Dictionary<string, object> ?? new Dictionary<string, object>();
        if (latencySeconds.HasValue)
        {
            updateMetadata["latency_seconds"] = latencySeconds.Value;
        }

        updateMetadata["success"] = success;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "trace-create",
                    body = new
                    {
                        id = traceId,
                        timestamp = timestamp.ToString("O"),
                        metadata = updateMetadata,
                        tags = success ? new[] { "success", "ai", "chat" } : new[] { "failure", "ai", "chat" }
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to end trace. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending trace");
        }
    }

    public async Task<string> StartSpanAsync(string traceId, string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var spanId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "span-create",
                    body = new
                    {
                        id = spanId,
                        traceId = traceId,
                        name = name,
                        startTime = timestamp.ToString("O"),
                        metadata = metadata,
                        environment = "production"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to start span. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting span");
        }

        return spanId;
    }

    public async Task EndSpanAsync(string traceId, string spanId, bool success,
        object? metadata = null, CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "span-update",
                    body = new
                    {
                        id = spanId,
                        endTime = timestamp.ToString("O"),
                        metadata = metadata,
                        level = success ? "DEFAULT" : "ERROR"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to end span. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending span");
        }
    }

    public async Task<string> RecordGenerationAsync(string traceId, string model, object? input = null,
        object? output = null, object? metadata = null, CancellationToken cancellationToken = default)
    {
        var generationId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "generation-create",
                    body = new
                    {
                        id = generationId,
                        traceId = traceId,
                        name = $"Generation-{model}",
                        startTime = timestamp.ToString("O"),
                        model = model,
                        input = input,
                        output = output,
                        metadata = metadata,
                        environment = "production"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to record generation. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording generation");
        }

        return generationId;
    }

    public async Task RecordEventAsync(string traceId, string name, object? data = null,
        CancellationToken cancellationToken = default)
    {
        var eventObsId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "event-create",
                    body = new
                    {
                        id = eventObsId,
                        traceId = traceId,
                        name = name,
                        startTime = timestamp.ToString("O"),
                        metadata = data,
                        environment = "production"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to record event. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording event");
        }
    }

    public async Task RecordToolInvocationAsync(string traceId, string toolName, object? parameters = null,
        object? result = null, CancellationToken cancellationToken = default)
    {
        var spanId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "span-create",
                    body = new
                    {
                        id = spanId,
                        traceId = traceId,
                        name = $"Tool: {toolName}",
                        startTime = timestamp.ToString("O"),
                        input = parameters,
                        output = result,
                        metadata = new
                        {
                            tool_name = toolName,
                            tool_type = "function_call"
                        },
                        environment = "production"
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to record tool invocation. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording tool invocation");
        }
    }

    public async Task UpdateTraceAsync(string traceId, object? input = null, object? output = null,
        object? metadata = null, CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid().ToString();
        var timestamp = DateTimeOffset.UtcNow;

        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = timestamp.ToString("O"),
                    type = "trace-create",
                    body = new
                    {
                        id = traceId,
                        timestamp = timestamp.ToString("O"),
                        input = input,
                        output = output,
                        metadata = metadata
                    }
                }
            }
        };

        try
        {
            var response = await _http.PostAsJsonAsync("api/public/ingestion", ingestionEvent, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode != HttpStatusCode.OK &&
                response.StatusCode != HttpStatusCode.MultiStatus)
            {
                _logger.LogWarning("Failed to update trace. Status: {StatusCode}", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating trace");
        }
    }
}