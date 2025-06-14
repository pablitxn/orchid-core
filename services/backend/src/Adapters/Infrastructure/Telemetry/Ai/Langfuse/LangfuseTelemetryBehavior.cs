using Application.Interfaces;
using MediatR;

namespace Infrastructure.Telemetry.Ai.Langfuse;

/// <summary>
///     MediatR pipeline behavior that wraps each request in Langfuse telemetry traces and spans.
/// </summary>
public class LangfuseTelemetryBehavior<TRequest, TResponse>(ITelemetryClient client)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ITelemetryClient _client = client ?? throw new ArgumentNullException(nameof(client));

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        // Start a new trace for this request
        var traceId = await _client.StartTraceAsync(
            requestName,
            new { request = requestName },
            cancellationToken).ConfigureAwait(false);
        string? spanId = null;
        try
        {
            // Start a handler execution span
            spanId = await _client.StartSpanAsync(
                traceId,
                "HandlerExecution",
                new { handler = typeof(TRequest).Name },
                cancellationToken).ConfigureAwait(false);
            // Execute the next handler in pipeline
            var response = await next().ConfigureAwait(false);
            // End span and trace as successful
            await _client.EndSpanAsync(traceId, spanId, true, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            await _client.EndTraceAsync(traceId, true, cancellationToken: cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch
        {
            // End span and trace as failed
            if (spanId is not null)
                await _client.EndSpanAsync(traceId, spanId, false, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

            await _client.EndTraceAsync(traceId, false, cancellationToken: cancellationToken).ConfigureAwait(false);
            throw;
        }
    }
}