using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Http;

/// <summary>
///     HTTP message handler that propagates the correlation ID to outgoing HTTP requests.
///     This ensures that downstream services (like OpenAI) receive the correlation ID
///     for distributed tracing purposes.
/// </summary>
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    public const string CorrelationIdHeaderName = "X-Correlation-ID";

    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<CorrelationIdDelegatingHandler> _logger;

    public CorrelationIdDelegatingHandler(
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<CorrelationIdDelegatingHandler> logger)
    {
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _correlationIdAccessor.CorrelationId;

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            // Add correlation ID to outgoing request headers
            request.Headers.TryAddWithoutValidation(CorrelationIdHeaderName, correlationId);

            _logger.LogDebug(
                "Propagating CorrelationId {CorrelationId} to outgoing request: {Method} {Uri}",
                correlationId,
                request.Method,
                request.RequestUri);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
