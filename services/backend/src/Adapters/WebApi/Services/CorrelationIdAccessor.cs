using Application.Interfaces;

namespace WebApi.Services;

/// <summary>
///     Scoped service that provides access to the current request's correlation ID.
///     Uses AsyncLocal to support async flows while maintaining request isolation.
/// </summary>
public class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private static readonly AsyncLocal<string?> CorrelationIdHolder = new();

    /// <inheritdoc />
    public string? CorrelationId => CorrelationIdHolder.Value;

    /// <inheritdoc />
    public void SetCorrelationId(string correlationId)
    {
        CorrelationIdHolder.Value = correlationId;
    }
}
