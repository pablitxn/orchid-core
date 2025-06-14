using Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Providers;

/// <summary>
///     Stores huddle video segments using an underlying <see cref="IFileStorageService" />.
/// </summary>
public class FileHuddleRecordingService(IFileStorageService storage, ILogger<FileHuddleRecordingService> logger)
    : IHuddleRecordingService
{
    private readonly ILogger<FileHuddleRecordingService> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IFileStorageService _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    /// <inheritdoc />
    public async Task StoreSegmentAsync(string roomId, Stream segment, CancellationToken cancellationToken = default)
    {
        var name = $"huddle-{roomId}-{DateTime.UtcNow:yyyyMMddHHmmssfff}.webm";
        await _storage.StoreFileAsync(segment, name, "video/webm");
        _logger.LogInformation("Stored huddle segment {File}", name);
    }
}