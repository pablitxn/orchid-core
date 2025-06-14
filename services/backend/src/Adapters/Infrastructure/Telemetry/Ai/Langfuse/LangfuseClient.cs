using System.Net;
using System.Net.Http.Json;
using Application.Interfaces;

namespace Infrastructure.Telemetry.Ai.Langfuse;

/// <summary>
///     Client for sending telemetry data to Langfuse via REST API.
/// </summary>
public class LangfuseClient(HttpClient http) : ITelemetryClient
{
    private readonly HttpClient _http = http ?? throw new ArgumentNullException(nameof(http));

    public async Task<string> StartTraceAsync(string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // Generate a unique ID for the trace
        var traceId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        
        // Create the ingestion event according to Langfuse API spec
        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    type = "trace-create",
                    body = new
                    {
                        id = traceId,
                        timestamp = DateTimeOffset.UtcNow.ToString("O"),
                        name = name,
                        metadata = metadata,
                        environment = "production"
                    }
                }
            }
        };
        
        var response = await _http.PostAsJsonAsync("/api/public/ingestion", ingestionEvent, cancellationToken).ConfigureAwait(false);
        
        // Langfuse returns 207 for partial success
        if (response.StatusCode != HttpStatusCode.OK && 
            response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw new HttpRequestException($"Failed to start trace. Status: {response.StatusCode}");
        }
        
        return traceId;
    }

    public async Task EndTraceAsync(string traceId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        // For Langfuse, we update the trace by sending another event with the same trace ID
        var eventId = Guid.NewGuid().ToString();
        
        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    type = "trace-create",
                    body = new
                    {
                        id = traceId,
                        timestamp = DateTimeOffset.UtcNow.ToString("O"),
                        metadata = metadata,
                        // Langfuse doesn't have a direct "success" field, we can put it in metadata
                        tags = success ? new[] { "success" } : new[] { "failure" }
                    }
                }
            }
        };
        
        var response = await _http.PostAsJsonAsync("/api/public/ingestion", ingestionEvent, cancellationToken)
            .ConfigureAwait(false);
        
        if (response.StatusCode != HttpStatusCode.OK && 
            response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw new HttpRequestException($"Failed to end trace. Status: {response.StatusCode}");
        }
    }

    public async Task<string> StartSpanAsync(string traceId, string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var spanId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid().ToString();
        
        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    type = "span-create",
                    body = new
                    {
                        id = spanId,
                        traceId = traceId,
                        name = name,
                        startTime = DateTimeOffset.UtcNow.ToString("O"),
                        metadata = metadata,
                        environment = "production"
                    }
                }
            }
        };
        
        var response = await _http.PostAsJsonAsync("/api/public/ingestion", ingestionEvent, cancellationToken).ConfigureAwait(false);
        
        if (response.StatusCode != HttpStatusCode.OK && 
            response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw new HttpRequestException($"Failed to start span. Status: {response.StatusCode}");
        }
        
        return spanId;
    }

    public async Task EndSpanAsync(string traceId, string spanId, bool success,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var eventId = Guid.NewGuid().ToString();
        
        var ingestionEvent = new
        {
            batch = new[]
            {
                new
                {
                    id = eventId,
                    timestamp = DateTimeOffset.UtcNow.ToString("O"),
                    type = "span-update",
                    body = new
                    {
                        id = spanId,
                        endTime = DateTimeOffset.UtcNow.ToString("O"),
                        metadata = metadata,
                        // Add success/failure indication through metadata or level
                        level = success ? "DEFAULT" : "ERROR"
                    }
                }
            }
        };
        
        var response = await _http.PostAsJsonAsync("/api/public/ingestion", ingestionEvent, cancellationToken)
            .ConfigureAwait(false);
        
        if (response.StatusCode != HttpStatusCode.OK && 
            response.StatusCode != HttpStatusCode.MultiStatus)
        {
            throw new HttpRequestException($"Failed to end span. Status: {response.StatusCode}");
        }
    }
}