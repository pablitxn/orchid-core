namespace Application.Interfaces;

/// <summary>
///     Provides access to the current request's correlation ID for cross-cutting concerns.
///     This port enables correlation ID propagation across application layers,
///     including AI calls, message publishing, and logging.
/// </summary>
public interface ICorrelationIdAccessor
{
    /// <summary>
    ///     Gets the correlation ID for the current request/operation context.
    ///     Returns null if no correlation ID has been established.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    ///     Sets the correlation ID for the current context.
    ///     Typically called by middleware at the start of a request.
    /// </summary>
    /// <param name="correlationId">The correlation ID to set.</param>
    void SetCorrelationId(string correlationId);
}
