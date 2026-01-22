using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Entities;

public class DocumentEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var document = new DocumentEntity
        {
            Id = id,
            SessionId = "session-123",
            DocumentPath = "/uploads/doc.pdf",
            FileName = "doc.pdf",
            ContentType = "application/pdf",
            FileSize = 1024000,
            Enum = DocumentEnum.KnowledgeBase,
            IsIndexed = true,
            ChunkCount = 10
        };

        // Assert
        Assert.Equal(id, document.Id);
        Assert.Equal("session-123", document.SessionId);
        Assert.Equal("/uploads/doc.pdf", document.DocumentPath);
        Assert.Equal("doc.pdf", document.FileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(1024000, document.FileSize);
        Assert.Equal(DocumentEnum.KnowledgeBase, document.Enum);
        Assert.True(document.IsIndexed);
        Assert.Equal(10, document.ChunkCount);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var document = new DocumentEntity();

        // Assert
        Assert.Equal(string.Empty, document.SessionId);
        Assert.Equal(string.Empty, document.DocumentPath);
        Assert.Equal(string.Empty, document.FileName);
        Assert.Equal(string.Empty, document.ContentType);
        Assert.Equal(DocumentEnum.Attachment, document.Enum);
        Assert.False(document.IsIndexed);
        Assert.Equal(0, document.ChunkCount);
        Assert.Null(document.Embedding);
        Assert.Empty(document.SheetChunks);
    }

    [Fact]
    public void SheetChunks_CanBeModified()
    {
        // Arrange
        var document = new DocumentEntity { Id = Guid.NewGuid() };
        var chunk = new SheetChunkEntity
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            SheetName = "Sheet1",
            StartRow = 1,
            EndRow = 100,
            Text = "Sample data"
        };

        // Act
        document.SheetChunks.Add(chunk);

        // Assert
        Assert.Single(document.SheetChunks);
        Assert.Equal(chunk, document.SheetChunks[0]);
    }
}
