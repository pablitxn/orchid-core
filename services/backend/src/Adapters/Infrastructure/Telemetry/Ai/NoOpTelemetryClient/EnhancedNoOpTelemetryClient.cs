using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Telemetry.Ai.NoOpTelemetryClient;

/// <summary>
/// Enhanced no-op telemetry client for non-AI telemetry scenarios
/// Logs telemetry calls at debug level without sending to external services
/// </summary>
public class EnhancedNoOpTelemetryClient(ILogger<EnhancedNoOpTelemetryClient> logger) : IEnhancedTelemetryClient
{
    private readonly ILogger<EnhancedNoOpTelemetryClient> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public Task<string> StartTraceAsync(string name, string? sessionId = null, string? userId = null,
        object? metadata = null, CancellationToken cancellationToken = default)
    {
        var traceId = Guid.NewGuid().ToString();
        _logger.LogDebug("NoOp: StartTrace {TraceId} - {Name}, SessionId: {SessionId}, UserId: {UserId}",
            traceId, name, sessionId, userId);
        return Task.FromResult(traceId);
    }

    public Task<string> StartTraceAsync(string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        return StartTraceAsync(name, null, null, metadata, cancellationToken);
    }

    public Task EndTraceAsync(string traceId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOp: EndTrace {TraceId} - Success: {Success}", traceId, success);
        return Task.CompletedTask;
    }

    public Task<string> StartSpanAsync(string traceId, string name, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var spanId = Guid.NewGuid().ToString();
        _logger.LogDebug("NoOp: StartSpan {SpanId} in Trace {TraceId} - {Name}", spanId, traceId, name);
        return Task.FromResult(spanId);
    }

    public Task EndSpanAsync(string traceId, string spanId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOp: EndSpan {SpanId} in Trace {TraceId} - Success: {Success}",
            spanId, traceId, success);
        return Task.CompletedTask;
    }

    public Task<string> RecordGenerationAsync(string traceId, string model, object? input = null,
        object? output = null, object? metadata = null, CancellationToken cancellationToken = default)
    {
        var generationId = Guid.NewGuid().ToString();
        _logger.LogDebug("NoOp: RecordGeneration {GenerationId} in Trace {TraceId} - Model: {Model}",
            generationId, traceId, model);
        return Task.FromResult(generationId);
    }

    public Task RecordEventAsync(string traceId, string name, object? data = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOp: RecordEvent in Trace {TraceId} - {Name}", traceId, name);
        return Task.CompletedTask;
    }

    public Task RecordToolInvocationAsync(string traceId, string toolName, object? parameters = null,
        object? result = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOp: RecordToolInvocation in Trace {TraceId} - Tool: {ToolName}",
            traceId, toolName);
        return Task.CompletedTask;
    }

    public Task UpdateTraceAsync(string traceId, object? input = null, object? output = null,
        object? metadata = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("NoOp: UpdateTrace {TraceId}", traceId);
        return Task.CompletedTask;
    }
}