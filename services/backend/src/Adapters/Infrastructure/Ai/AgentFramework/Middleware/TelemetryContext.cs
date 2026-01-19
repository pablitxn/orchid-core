namespace Infrastructure.Ai.AgentFramework.Middleware;

/// <summary>
/// Ambient context for telemetry data that flows through the tool invocation chain.
/// Uses AsyncLocal to propagate traceId and sessionId across async boundaries.
/// </summary>
public static class TelemetryContext
{
    private static readonly AsyncLocal<TelemetryScope?> CurrentScope = new();

    /// <summary>
    /// Gets the current trace ID, or null if not within a telemetry scope.
    /// </summary>
    public static string? TraceId => CurrentScope.Value?.TraceId;

    /// <summary>
    /// Gets the current session ID, or null if not within a telemetry scope.
    /// </summary>
    public static string? SessionId => CurrentScope.Value?.SessionId;

    /// <summary>
    /// Gets the current user ID, or null if not within a telemetry scope.
    /// </summary>
    public static string? UserId => CurrentScope.Value?.UserId;

    /// <summary>
    /// Indicates whether telemetry is currently enabled for this scope.
    /// </summary>
    public static bool IsEnabled => CurrentScope.Value?.IsEnabled ?? false;

    /// <summary>
    /// Creates a new telemetry scope that will be active for the duration of the returned IDisposable.
    /// </summary>
    /// <param name="traceId">The trace ID for this scope.</param>
    /// <param name="sessionId">The session ID for this scope.</param>
    /// <param name="userId">The user ID for this scope.</param>
    /// <returns>An IDisposable that restores the previous scope when disposed.</returns>
    public static IDisposable BeginScope(string traceId, string? sessionId = null, string? userId = null)
    {
        var previousScope = CurrentScope.Value;
        CurrentScope.Value = new TelemetryScope(traceId, sessionId, userId, true);
        return new ScopeDisposer(previousScope);
    }

    /// <summary>
    /// Creates a disabled telemetry scope (useful for testing or when telemetry should be skipped).
    /// </summary>
    public static IDisposable BeginDisabledScope()
    {
        var previousScope = CurrentScope.Value;
        CurrentScope.Value = new TelemetryScope(null, null, null, false);
        return new ScopeDisposer(previousScope);
    }

    private sealed class ScopeDisposer : IDisposable
    {
        private readonly TelemetryScope? _previousScope;
        private bool _disposed;

        public ScopeDisposer(TelemetryScope? previousScope)
        {
            _previousScope = previousScope;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentScope.Value = _previousScope;
        }
    }

    private sealed record TelemetryScope(
        string? TraceId,
        string? SessionId,
        string? UserId,
        bool IsEnabled);
}
