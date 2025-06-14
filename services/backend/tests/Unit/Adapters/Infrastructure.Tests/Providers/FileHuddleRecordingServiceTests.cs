using Application.Interfaces;
using Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Providers;

public class FileHuddleRecordingServiceTests
{
    [Fact]
    public async Task StoreSegmentAsync_DelegatesToFileStorage()
    {
        var storage = new Mock<IFileStorageService>();
        var logger = new Mock<ILogger<FileHuddleRecordingService>>();
        var service = new FileHuddleRecordingService(storage.Object, logger.Object);

        using var ms = new MemoryStream(new byte[] { 1, 2, 3 });
        await service.StoreSegmentAsync("room1", ms);

        storage.Verify(s => s.StoreFileAsync(It.IsAny<Stream>(), It.Is<string>(n => n.Contains("room1")), "video/webm"),
            Times.Once);
    }
}