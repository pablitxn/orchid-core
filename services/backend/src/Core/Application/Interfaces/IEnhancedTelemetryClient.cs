namespace Application.Interfaces;

/// <summary>
/// Enhanced telemetry client interface with session support for Langfuse integration
/// </summary>
public interface IEnhancedTelemetryClient : ITelemetryClient
{
    /// <summary>
    /// Starts a new trace with session context
    /// </summary>
    Task<string> StartTraceAsync(string name, string? sessionId = null, string? userId = null, 
        object? metadata = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records a generation (LLM completion) within a trace
    /// </summary>
    Task<string> RecordGenerationAsync(string traceId, string model, object? input = null, 
        object? output = null, object? metadata = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records an event within a trace
    /// </summary>
    Task RecordEventAsync(string traceId, string name, object? data = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Records tool invocation activity
    /// </summary>
    Task RecordToolInvocationAsync(string traceId, string toolName, object? parameters = null, 
        object? result = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates trace with input/output data
    /// </summary>
    Task UpdateTraceAsync(string traceId, object? input = null, object? output = null, 
        object? metadata = null, CancellationToken cancellationToken = default);
}