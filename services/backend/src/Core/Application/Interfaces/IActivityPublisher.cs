namespace Application.Interfaces;

/// <summary>
///     Publishes activity events to connected clients.
/// </summary>
public interface IActivityPublisher
{
    /// <summary>
    ///     Publishes an activity with the given type and optional payload.
    /// </summary>
    Task PublishAsync(string type, object? payload = null, CancellationToken cancellationToken = default);
}