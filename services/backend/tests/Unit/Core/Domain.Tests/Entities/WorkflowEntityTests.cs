using Domain.Entities;

namespace Domain.Tests.Entities;

public class WorkflowEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var publishedAt = DateTime.UtcNow;

        var workflow = new WorkflowEntity
        {
            Id = id,
            Name = "Content Generation",
            Description = "Generate blog content",
            PriceCredits = 50,
            Author = "Admin",
            Category = "Content",
            Steps = 5,
            EstimatedTime = "10 minutes",
            Rating = 4.5,
            Runs = 100,
            Icon = "pencil",
            Tags = "blog,content,writing",
            DetailedDescription = "A comprehensive content generation workflow...",
            Prerequisites = "None",
            InputRequirements = "Topic and keywords",
            OutputFormat = "Markdown",
            IsPublic = true,
            IsActive = true,
            PublishedAt = publishedAt
        };

        // Assert
        Assert.Equal(id, workflow.Id);
        Assert.Equal("Content Generation", workflow.Name);
        Assert.Equal("Generate blog content", workflow.Description);
        Assert.Equal(50, workflow.PriceCredits);
        Assert.Equal("Admin", workflow.Author);
        Assert.Equal("Content", workflow.Category);
        Assert.Equal(5, workflow.Steps);
        Assert.Equal("10 minutes", workflow.EstimatedTime);
        Assert.Equal(4.5, workflow.Rating);
        Assert.Equal(100, workflow.Runs);
        Assert.Equal("pencil", workflow.Icon);
        Assert.Equal("blog,content,writing", workflow.Tags);
        Assert.Equal("A comprehensive content generation workflow...", workflow.DetailedDescription);
        Assert.Equal("None", workflow.Prerequisites);
        Assert.Equal("Topic and keywords", workflow.InputRequirements);
        Assert.Equal("Markdown", workflow.OutputFormat);
        Assert.True(workflow.IsPublic);
        Assert.True(workflow.IsActive);
        Assert.Equal(publishedAt, workflow.PublishedAt);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var workflow = new WorkflowEntity();

        // Assert
        Assert.Equal(string.Empty, workflow.Name);
        Assert.Null(workflow.Description);
        Assert.Equal(1, workflow.Steps);
        Assert.Equal(0.0, workflow.Rating);
        Assert.Equal(0, workflow.Runs);
        Assert.False(workflow.IsPublic);
        Assert.True(workflow.IsActive);
        Assert.Null(workflow.PublishedAt);
    }
}
