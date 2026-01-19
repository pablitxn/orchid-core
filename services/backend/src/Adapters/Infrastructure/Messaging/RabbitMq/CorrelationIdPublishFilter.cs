using Application.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;
using SerilogContext = Serilog.Context.LogContext;

namespace Infrastructure.Messaging.RabbitMq;

/// <summary>
///     MassTransit publish filter that propagates the correlation ID to message headers.
///     This ensures distributed tracing across message-based communication.
/// </summary>
public class CorrelationIdPublishFilter<T> : IFilter<PublishContext<T>> where T : class
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CorrelationIdPublishFilter<T>> _logger;

    public CorrelationIdPublishFilter(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CorrelationIdPublishFilter<T>> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationId");
    }

    public async Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Set the correlation ID in the message context
            // MassTransit will propagate this in message headers
            context.CorrelationId = Guid.TryParse(correlationId, out var guidId)
                ? guidId
                : Guid.Parse(correlationId.PadRight(32, '0')[..32].Insert(8, "-").Insert(13, "-").Insert(18, "-").Insert(23, "-"));

            // Also add as a custom header for non-GUID correlation IDs
            context.Headers.Set("X-Correlation-ID", correlationId);

            _logger.LogDebug(
                "Publishing message with CorrelationId {CorrelationId}. MessageType: {MessageType}",
                correlationId,
                typeof(T).Name);
        }

        await next.Send(context);
    }
}

/// <summary>
///     MassTransit consume filter that extracts the correlation ID from incoming messages
///     and pushes it to the Serilog LogContext.
/// </summary>
public class CorrelationIdConsumeFilter<T> : IFilter<ConsumeContext<T>> where T : class
{
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CorrelationIdConsumeFilter<T>> _logger;

    public CorrelationIdConsumeFilter(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CorrelationIdConsumeFilter<T>> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("correlationId");
    }

    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        // Try to get correlation ID from custom header first, then fall back to MassTransit's CorrelationId
        var correlationId = context.Headers.Get<string>("X-Correlation-ID")
                            ?? context.CorrelationId?.ToString()
                            ?? Guid.NewGuid().ToString("N")[..12];

        // Set in the accessor for downstream services
        _correlationIdAccessor.SetCorrelationId(correlationId);

        _logger.LogDebug(
            "Consuming message with CorrelationId {CorrelationId}. MessageType: {MessageType}",
            correlationId,
            typeof(T).Name);

        // Push to Serilog LogContext (use alias to avoid ambiguity with MassTransit.LogContext)
        using (SerilogContext.PushProperty("CorrelationId", correlationId))
        {
            await next.Send(context);
        }
    }
}

/// <summary>
///     Extension methods to configure MassTransit with correlation ID filters.
/// </summary>
public static class CorrelationIdFilterExtensions
{
    /// <summary>
    ///     Adds correlation ID propagation filters to MassTransit.
    /// </summary>
    public static void UseCorrelationIdFilters(this IBusFactoryConfigurator configurator, IRegistrationContext context)
    {
        configurator.UsePublishFilter(typeof(CorrelationIdPublishFilter<>), context);
        configurator.UseConsumeFilter(typeof(CorrelationIdConsumeFilter<>), context);
    }
}
