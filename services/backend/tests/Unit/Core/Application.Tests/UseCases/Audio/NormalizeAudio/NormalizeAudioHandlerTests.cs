using Application.Interfaces;
using Application.UseCases.Audio.NormalizeAudio;
using Moq;

namespace Application.Tests.UseCases.Audio.NormalizeAudio;

public class NormalizeAudioCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidCommand_CallsDependencies()
    {
        // Arrange
        var audioData = new byte[] { 0x01, 0x02 };
        var command = new NormalizeAudioCommand(Guid.NewGuid(), audioData, "sample.wav");

        var mockNormalizer = new Mock<IAudioNormalizer>();
        var mockFileStorage = new Mock<IFileStorageService>();

        // Setup mocks to return dummy values
        mockNormalizer.Setup(n => n.ConvertToMp3Async(It.IsAny<byte[]>()))
            .ReturnsAsync([0x03, 0x04]);
        mockFileStorage.Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("https://cloudstorage/file.mp3");

        var handler = new NormalizeAudioHandler(mockNormalizer.Object, mockFileStorage.Object);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.Equal("https://cloudstorage/file.mp3", result);
        mockNormalizer.Verify(n => n.ConvertToMp3Async(audioData), Times.Once);
        mockFileStorage.Verify(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }
}