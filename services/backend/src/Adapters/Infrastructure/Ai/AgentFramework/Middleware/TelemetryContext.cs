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
    /// Gets the current agent ID, or null if not within a telemetry scope.
    /// </summary>
    public static string? AgentId => CurrentScope.Value?.AgentId;

    /// <summary>
    /// Indicates whether telemetry is currently enabled for this scope.
    /// </summary>
    public static bool IsEnabled => CurrentScope.Value?.IsEnabled ?? false;

    /// <summary>
    /// Gets the function-to-tool mapping for credit tracking.
    /// Maps function names to their tool system names.
    /// </summary>
    internal static IReadOnlyDictionary<string, string>? FunctionToolMapping =>
        CurrentScope.Value?.FunctionToolMapping;

    /// <summary>
    /// Gets the tool system name for a given function name.
    /// </summary>
    /// <param name="functionName">The name of the function.</param>
    /// <returns>The tool system name if found, null otherwise.</returns>
    public static string? GetToolSystemName(string functionName)
    {
        if (CurrentScope.Value?.FunctionToolMapping == null)
            return null;

        return CurrentScope.Value.FunctionToolMapping.TryGetValue(functionName, out var systemName)
            ? systemName
            : null;
    }

    /// <summary>
    /// Creates a new telemetry scope that will be active for the duration of the returned IDisposable.
    /// </summary>
    /// <param name="traceId">The trace ID for this scope.</param>
    /// <param name="sessionId">The session ID for this scope.</param>
    /// <param name="userId">The user ID for this scope.</param>
    /// <param name="agentId">The agent ID for this scope.</param>
    /// <param name="functionToolMapping">Optional mapping of function names to tool system names for credit tracking.</param>
    /// <returns>An IDisposable that restores the previous scope when disposed.</returns>
    public static IDisposable BeginScope(
        string traceId,
        string? sessionId = null,
        string? userId = null,
        string? agentId = null,
        IReadOnlyDictionary<string, string>? functionToolMapping = null)
    {
        var previousScope = CurrentScope.Value;
        CurrentScope.Value = new TelemetryScope(traceId, sessionId, userId, agentId, true, functionToolMapping);
        return new ScopeDisposer(previousScope);
    }

    /// <summary>
    /// Creates a disabled telemetry scope (useful for testing or when telemetry should be skipped).
    /// </summary>
    public static IDisposable BeginDisabledScope()
    {
        var previousScope = CurrentScope.Value;
        CurrentScope.Value = new TelemetryScope(null, null, null, null, false, null);
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
        string? AgentId,
        bool IsEnabled,
        IReadOnlyDictionary<string, string>? FunctionToolMapping);
}
