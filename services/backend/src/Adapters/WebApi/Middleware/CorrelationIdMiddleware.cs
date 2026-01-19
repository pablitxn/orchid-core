using Application.Interfaces;
using Serilog.Context;

namespace WebApi.Middleware;

/// <summary>
///     Middleware that ensures each HTTP request has a correlation ID for distributed tracing.
///     The correlation ID is:
///     - Extracted from the incoming X-Correlation-ID header if present
///     - Generated as a new GUID if not provided
///     - Added to the response headers for client debugging
///     - Pushed to the Serilog LogContext for all logs within the request
///     - Made available via ICorrelationIdAccessor for downstream services
/// </summary>
public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        // Extract correlation ID from header or generate a new one
        var correlationId = context.Request.Headers[CorrelationIdHeaderName].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N")[..12]; // Short format: abc123def456
        }

        // Store in HttpContext.Items for easy access
        context.Items[CorrelationIdItemKey] = correlationId;

        // Set in the accessor for DI-based access
        correlationIdAccessor.SetCorrelationId(correlationId);

        // Add to response headers for client debugging
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeaderName] = correlationId;
            return Task.CompletedTask;
        });

        // Push to Serilog LogContext so all logs within this request include the correlation ID
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            _logger.LogDebug("Request started with CorrelationId: {CorrelationId}", correlationId);

            await _next(context);

            _logger.LogDebug("Request completed with CorrelationId: {CorrelationId}", correlationId);
        }
    }
}

/// <summary>
///     Extension methods for adding the CorrelationIdMiddleware to the request pipeline.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    /// <summary>
    ///     Adds the correlation ID middleware to the application's request pipeline.
    ///     Should be added early in the pipeline, before other middleware that may need the correlation ID.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}
