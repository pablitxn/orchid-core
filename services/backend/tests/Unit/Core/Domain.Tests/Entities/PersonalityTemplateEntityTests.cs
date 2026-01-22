using Core.Domain.Entities;

namespace Domain.Tests.Entities;

public class PersonalityTemplateEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var template = new PersonalityTemplateEntity
        {
            Id = id,
            Name = "Professional Assistant",
            Description = "A professional and formal assistant",
            Prompt = "You are a professional assistant...",
            Category = "Business",
            IsSystem = true,
            DisplayOrder = 1,
            IsActive = true
        };

        // Assert
        Assert.Equal(id, template.Id);
        Assert.Equal("Professional Assistant", template.Name);
        Assert.Equal("A professional and formal assistant", template.Description);
        Assert.Equal("You are a professional assistant...", template.Prompt);
        Assert.Equal("Business", template.Category);
        Assert.True(template.IsSystem);
        Assert.Equal(1, template.DisplayOrder);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var template = new PersonalityTemplateEntity();

        // Assert
        Assert.Equal(string.Empty, template.Name);
        Assert.Equal(string.Empty, template.Description);
        Assert.Equal(string.Empty, template.Prompt);
        Assert.Equal(string.Empty, template.Category);
        Assert.False(template.IsSystem);
        Assert.Equal(0, template.DisplayOrder);
        Assert.True(template.IsActive);
        Assert.Empty(template.Agents);
    }
}
