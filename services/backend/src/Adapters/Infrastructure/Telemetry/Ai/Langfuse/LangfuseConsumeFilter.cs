using Application.Interfaces;
using MassTransit;

namespace Infrastructure.Telemetry.Ai.Langfuse;

/// <summary>
///     MassTransit consume filter that wraps message processing in Langfuse spans.
/// </summary>
public class LangfuseConsumeFilter<T>(ITelemetryClient client) : IFilter<ConsumeContext<T>>
    where T : class
{
    private readonly ITelemetryClient _client = client;

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        var messageName = typeof(T).Name;
        var traceId = await _client.StartTraceAsync(
                messageName,
                new { message = messageName, id = context.MessageId },
                context.CancellationToken)
            .ConfigureAwait(false);
        string? spanId = null;
        try
        {
            spanId = await _client.StartSpanAsync(
                    traceId,
                    "Consume",
                    new { consumer = context.DestinationAddress?.AbsolutePath },
                    context.CancellationToken)
                .ConfigureAwait(false);
            await next.Send(context).ConfigureAwait(false);
            if (spanId is not null)
                await _client.EndSpanAsync(traceId, spanId, true, cancellationToken: context.CancellationToken)
                    .ConfigureAwait(false);
            await _client.EndTraceAsync(traceId, true, cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            if (spanId is not null)
                await _client.EndSpanAsync(traceId, spanId, false, cancellationToken: context.CancellationToken)
                    .ConfigureAwait(false);
            await _client.EndTraceAsync(traceId, false, cancellationToken: context.CancellationToken)
                .ConfigureAwait(false);
            throw;
        }
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("langfuseConsume");
    }
}