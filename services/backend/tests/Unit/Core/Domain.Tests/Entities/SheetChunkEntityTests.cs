using Domain.Entities;

namespace Domain.Tests.Entities;

public class SheetChunkEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var chunk = new SheetChunkEntity
        {
            Id = id,
            DocumentId = documentId,
            SheetName = "Sheet1",
            StartRow = 1,
            EndRow = 50,
            Text = "Row 1 data, Row 2 data..."
        };

        // Assert
        Assert.Equal(id, chunk.Id);
        Assert.Equal(documentId, chunk.DocumentId);
        Assert.Equal("Sheet1", chunk.SheetName);
        Assert.Equal(1, chunk.StartRow);
        Assert.Equal(50, chunk.EndRow);
        Assert.Equal("Row 1 data, Row 2 data...", chunk.Text);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var chunk = new SheetChunkEntity();

        // Assert
        Assert.Equal(string.Empty, chunk.SheetName);
        Assert.Equal(string.Empty, chunk.Text);
        Assert.Equal(0, chunk.StartRow);
        Assert.Equal(0, chunk.EndRow);
        Assert.Null(chunk.Embedding);
    }
}
