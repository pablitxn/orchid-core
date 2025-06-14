namespace Application.Interfaces;

/// <summary>
///     Stores video segments captured during a huddle session.
/// </summary>
public interface IHuddleRecordingService
{
    /// <summary>
    ///     Persists a recorded media segment for the given room.
    /// </summary>
    /// <param name="roomId">Identifier of the huddle room.</param>
    /// <param name="segment">Raw media data.</param>
    Task StoreSegmentAsync(string roomId, Stream segment, CancellationToken cancellationToken = default);
}