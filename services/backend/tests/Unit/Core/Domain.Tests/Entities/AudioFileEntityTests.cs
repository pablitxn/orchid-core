using Domain.Entities;

namespace Domain.Tests.Entities;

public class AudioFileEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var audioFile = new AudioFileEntity
        {
            Id = id,
            FileName = "recording.mp3",
            ContentType = "audio/mpeg",
            FileSize = 5242880,
            Url = "https://storage.example.com/recording.mp3",
            ProjectId = projectId
        };

        // Assert
        Assert.Equal(id, audioFile.Id);
        Assert.Equal("recording.mp3", audioFile.FileName);
        Assert.Equal("audio/mpeg", audioFile.ContentType);
        Assert.Equal(5242880, audioFile.FileSize);
        Assert.Equal("https://storage.example.com/recording.mp3", audioFile.Url);
        Assert.Equal(projectId, audioFile.ProjectId);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var audioFile = new AudioFileEntity();

        // Assert
        Assert.Equal(string.Empty, audioFile.FileName);
        Assert.Equal(string.Empty, audioFile.ContentType);
        Assert.Equal(string.Empty, audioFile.Url);
        Assert.Equal(0, audioFile.FileSize);
    }
}
