using Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Infrastructure.Telemetry.Ai.Langfuse;

/// <summary>
/// Enhanced MediatR pipeline behavior with session-aware Langfuse telemetry
/// </summary>
public class EnhancedLangfuseTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnhancedTelemetryClient _telemetryClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<EnhancedLangfuseTelemetryBehavior<TRequest, TResponse>> _logger;

    public EnhancedLangfuseTelemetryBehavior(
        IEnhancedTelemetryClient telemetryClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<EnhancedLangfuseTelemetryBehavior<TRequest, TResponse>> logger)
    {
        _telemetryClient = telemetryClient ?? throw new ArgumentNullException(nameof(telemetryClient));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip telemetry for non-AI operations
        if (!TelemetryOperationClassifier.IsAiOperation(request))
        {
            return await next();
        }

        var requestName = typeof(TRequest).Name;
        var (sessionId, userId) = ExtractSessionContext();

        // Start a new trace with session context
        var traceId = await _telemetryClient.StartTraceAsync(
            requestName,
            sessionId,
            userId,
            new 
            { 
                request = requestName,
                requestData = SerializeRequest(request),
                timestamp = DateTimeOffset.UtcNow
            },
            cancellationToken);

        string? spanId = null;
        try
        {
            // Start a handler execution span
            spanId = await _telemetryClient.StartSpanAsync(
                traceId,
                "HandlerExecution",
                new 
                { 
                    handler = typeof(TRequest).Name,
                    sessionId,
                    userId
                },
                cancellationToken);

            // Execute the next handler in pipeline
            var response = await next();

            // Record successful completion
            await _telemetryClient.EndSpanAsync(traceId, spanId, true, 
                new { responseType = typeof(TResponse).Name }, 
                cancellationToken);

            await _telemetryClient.EndTraceAsync(traceId, true, 
                new { responseData = SerializeResponse(response) }, 
                cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in handler {HandlerName}", requestName);

            // End span and trace as failed
            if (spanId is not null)
            {
                await _telemetryClient.EndSpanAsync(traceId, spanId, false, 
                    new { error = ex.Message, exceptionType = ex.GetType().Name }, 
                    cancellationToken);
            }

            await _telemetryClient.EndTraceAsync(traceId, false, 
                new { error = ex.Message, stackTrace = ex.StackTrace }, 
                cancellationToken);

            throw;
        }
    }

    private (string? sessionId, string? userId) ExtractSessionContext()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return (null, null);
        }

        // Try to get session ID from different sources
        string? sessionId = null;
        
        // 1. From query string (for SignalR connections)
        if (httpContext.Request.Query.TryGetValue("sessionId", out var querySessionId))
        {
            sessionId = querySessionId.ToString();
        }
        // 2. From header
        else if (httpContext.Request.Headers.TryGetValue("X-Session-Id", out var headerSessionId))
        {
            sessionId = headerSessionId.ToString();
        }
        // 3. From route data
        // else if (httpContext.Request.RouteValues.TryGetValue("sessionId", out var routeSessionId))
        // {
            // sessionId = routeSessionId?.ToString();
        // }

        // Get user ID from claims
        var userId = httpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return (sessionId, userId);
    }


    private object? SerializeRequest(TRequest request)
    {
        try
        {
            // Extract relevant properties for telemetry
            // Avoid serializing large objects like file contents
            var properties = new Dictionary<string, object?>();
            var requestType = request.GetType();

            foreach (var prop in requestType.GetProperties())
            {
                // Skip sensitive or large properties
                if (prop.Name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                    prop.Name.Contains("Content", StringComparison.OrdinalIgnoreCase) ||
                    prop.PropertyType == typeof(byte[]))
                {
                    continue;
                }

                try
                {
                    var value = prop.GetValue(request);
                    if (value != null)
                    {
                        properties[prop.Name] = value.ToString();
                    }
                }
                catch
                {
                    // Skip properties that throw exceptions
                }
            }

            return properties;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize request for telemetry");
            return new { type = typeof(TRequest).Name };
        }
    }

    private object? SerializeResponse(TResponse response)
    {
        try
        {
            if (response == null)
            {
                return null;
            }

            // For simple types, return as-is
            var responseType = typeof(TResponse);
            if (responseType.IsPrimitive || responseType == typeof(string))
            {
                return response;
            }

            // For complex types, extract summary information
            var summary = new Dictionary<string, object?>
            {
                ["type"] = responseType.Name
            };

            // Try to extract key properties
            var successProp = responseType.GetProperty("Success") ?? responseType.GetProperty("IsSuccess");
            if (successProp != null)
            {
                summary["success"] = successProp.GetValue(response);
            }

            var countProp = responseType.GetProperty("Count") ?? responseType.GetProperty("TotalCount");
            if (countProp != null)
            {
                summary["count"] = countProp.GetValue(response);
            }

            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to serialize response for telemetry");
            return new { type = typeof(TResponse).Name };
        }
    }
}