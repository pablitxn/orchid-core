namespace Application.Interfaces;

public interface ITelemetryClient
{
    Task<string> StartTraceAsync(string name, object? metadata = null, CancellationToken cancellationToken = default);

    Task EndTraceAsync(string traceId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default);

    Task<string> StartSpanAsync(string traceId, string name, object? metadata = null,
        CancellationToken cancellationToken = default);

    Task EndSpanAsync(string traceId, string spanId, bool success, object? metadata = null,
        CancellationToken cancellationToken = default);
}