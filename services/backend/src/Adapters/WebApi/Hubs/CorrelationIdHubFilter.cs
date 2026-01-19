using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Serilog.Context;
using WebApi.Middleware;

namespace WebApi.Hubs;

/// <summary>
///     Hub filter that ensures correlation ID propagation for SignalR hub method invocations.
///     Extracts the correlation ID from the connection context and pushes it to the Serilog LogContext.
/// </summary>
public class CorrelationIdHubFilter : IHubFilter
{
    private const string ConnectionCorrelationIdKey = "CorrelationId";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var correlationId = GetOrCreateCorrelationId(invocationContext.Context);

        // Set in the correlation ID accessor if available
        var accessor = invocationContext.ServiceProvider.GetService<ICorrelationIdAccessor>();
        accessor?.SetCorrelationId(correlationId);

        // Push to Serilog LogContext for structured logging
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            return await next(invocationContext);
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        var correlationId = ExtractOrGenerateCorrelationId(context.Context);

        // Store in connection items for future hub method invocations
        context.Context.Items[ConnectionCorrelationIdKey] = correlationId;

        // Set in the correlation ID accessor if available
        var accessor = context.ServiceProvider.GetService<ICorrelationIdAccessor>();
        accessor?.SetCorrelationId(correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<CorrelationIdHubFilter>>();
            logger.LogInformation(
                "SignalR connection established. ConnectionId: {ConnectionId}, UserId: {UserId}",
                context.Context.ConnectionId,
                context.Context.UserIdentifier);

            await next(context);
        }
    }

    /// <inheritdoc />
    public async Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        var correlationId = GetOrCreateCorrelationId(context.Context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<CorrelationIdHubFilter>>();

            if (exception != null)
            {
                logger.LogWarning(
                    exception,
                    "SignalR connection disconnected with error. ConnectionId: {ConnectionId}, UserId: {UserId}",
                    context.Context.ConnectionId,
                    context.Context.UserIdentifier);
            }
            else
            {
                logger.LogInformation(
                    "SignalR connection disconnected. ConnectionId: {ConnectionId}, UserId: {UserId}",
                    context.Context.ConnectionId,
                    context.Context.UserIdentifier);
            }

            await next(context, exception);
        }
    }

    private static string GetOrCreateCorrelationId(HubCallerContext context)
    {
        // Try to get from connection items (set during OnConnectedAsync)
        if (context.Items.TryGetValue(ConnectionCorrelationIdKey, out var storedId) &&
            storedId is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        // Fallback: generate a new one
        return GenerateCorrelationId();
    }

    private static string ExtractOrGenerateCorrelationId(HubCallerContext context)
    {
        // Try to get from HTTP context (set by CorrelationIdMiddleware during handshake)
        var httpContext = context.GetHttpContext();

        if (httpContext != null)
        {
            // First, check if the middleware already set it
            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var itemId) &&
                itemId is string existingId &&
                !string.IsNullOrWhiteSpace(existingId))
            {
                return existingId;
            }

            // Try to extract from query string (client may pass it)
            var queryCorrelationId = httpContext.Request.Query["correlationId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(queryCorrelationId))
            {
                return queryCorrelationId;
            }

            // Try to extract from header
            var headerCorrelationId = httpContext.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(headerCorrelationId))
            {
                return headerCorrelationId;
            }
        }

        // Generate a new correlation ID for this connection
        return GenerateCorrelationId();
    }

    private static string GenerateCorrelationId()
    {
        return Guid.NewGuid().ToString("N")[..12]; // Short format: abc123def456
    }
}
