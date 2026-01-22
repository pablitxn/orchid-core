using Domain.Entities;

namespace Domain.Tests.Entities;

public class MediaCenterAssetEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var knowledgeBaseFileId = Guid.NewGuid();
        var duration = TimeSpan.FromMinutes(5);

        var asset = new MediaCenterAssetEntity
        {
            Id = id,
            KnowledgeBaseFileId = knowledgeBaseFileId,
            MimeType = "video/mp4",
            Title = "Training Video",
            Duration = duration,
            FileUrl = "https://storage.example.com/video.mp4"
        };

        // Assert
        Assert.Equal(id, asset.Id);
        Assert.Equal(knowledgeBaseFileId, asset.KnowledgeBaseFileId);
        Assert.Equal("video/mp4", asset.MimeType);
        Assert.Equal("Training Video", asset.Title);
        Assert.Equal(duration, asset.Duration);
        Assert.Equal("https://storage.example.com/video.mp4", asset.FileUrl);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var asset = new MediaCenterAssetEntity();

        // Assert
        Assert.Equal(string.Empty, asset.MimeType);
        Assert.Equal(string.Empty, asset.Title);
        Assert.Equal(string.Empty, asset.FileUrl);
        Assert.Null(asset.Duration);
    }

    [Fact]
    public void Duration_IsOptional()
    {
        // Arrange - image asset without duration
        var asset = new MediaCenterAssetEntity
        {
            Id = Guid.NewGuid(),
            KnowledgeBaseFileId = Guid.NewGuid(),
            MimeType = "image/png",
            Title = "Screenshot",
            Duration = null,
            FileUrl = "https://storage.example.com/image.png"
        };

        // Assert
        Assert.Null(asset.Duration);
    }
}
