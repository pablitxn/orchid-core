using Domain.Entities;

namespace Domain.Tests.Entities;

public class KnowledgeBaseFileEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tags = new[] { "tag1", "tag2", "tag3" };

        var file = new KnowledgeBaseFileEntity
        {
            Id = id,
            UserId = userId,
            Title = "Test Document",
            Description = "A test document",
            Tags = tags,
            MimeType = "application/pdf",
            FileUrl = "https://storage.example.com/file.pdf",
            FileSize = 2048000
        };

        // Assert
        Assert.Equal(id, file.Id);
        Assert.Equal(userId, file.UserId);
        Assert.Equal("Test Document", file.Title);
        Assert.Equal("A test document", file.Description);
        Assert.Equal(tags, file.Tags);
        Assert.Equal("application/pdf", file.MimeType);
        Assert.Equal("https://storage.example.com/file.pdf", file.FileUrl);
        Assert.Equal(2048000, file.FileSize);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var file = new KnowledgeBaseFileEntity();

        // Assert
        Assert.Equal(string.Empty, file.Title);
        Assert.Null(file.Description);
        Assert.Empty(file.Tags);
        Assert.Equal(string.Empty, file.MimeType);
        Assert.Equal(string.Empty, file.FileUrl);
    }
}
