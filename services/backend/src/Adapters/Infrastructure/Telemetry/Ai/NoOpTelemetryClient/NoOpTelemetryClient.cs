using Application.Interfaces;

namespace Infrastructure.Telemetry.Ai.NoOpTelemetryClient
{
    /// <summary>
    /// No-op implementation of ITelemetryClient for scenarios where real telemetry is not configured.
    /// </summary>
    public class NoOpTelemetryClient : ITelemetryClient
    {
        public Task<string> StartTraceAsync(string name, object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task EndTraceAsync(string traceId, bool success, object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<string> StartSpanAsync(string traceId, string name, object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task EndSpanAsync(string traceId, string spanId, bool success, object? metadata = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}