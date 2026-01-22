using Core.Domain.Entities;
using Domain.Entities;

namespace Domain.Tests.Entities;

public class AgentEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var personalityTemplateId = Guid.NewGuid();
        var pluginIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var agent = new AgentEntity
        {
            Id = id,
            Name = "Test Agent",
            Description = "A test agent",
            AvatarUrl = "https://example.com/avatar.png",
            Personality = "Friendly and helpful",
            PersonalityTemplateId = personalityTemplateId,
            Language = "en",
            Model = "gpt-4",
            PluginIds = pluginIds,
            UserId = userId,
            IsPublic = true,
            IsDeleted = false,
            IsInRecycleBin = false
        };

        // Assert
        Assert.Equal(id, agent.Id);
        Assert.Equal("Test Agent", agent.Name);
        Assert.Equal("A test agent", agent.Description);
        Assert.Equal("https://example.com/avatar.png", agent.AvatarUrl);
        Assert.Equal("Friendly and helpful", agent.Personality);
        Assert.Equal(personalityTemplateId, agent.PersonalityTemplateId);
        Assert.Equal("en", agent.Language);
        Assert.Equal("gpt-4", agent.Model);
        Assert.Equal(pluginIds, agent.PluginIds);
        Assert.Equal(userId, agent.UserId);
        Assert.True(agent.IsPublic);
        Assert.False(agent.IsDeleted);
        Assert.False(agent.IsInRecycleBin);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var agent = new AgentEntity();

        // Assert
        Assert.Equal(string.Empty, agent.Name);
        Assert.Empty(agent.PluginIds);
        Assert.False(agent.IsPublic);
        Assert.False(agent.IsDeleted);
        Assert.False(agent.IsInRecycleBin);
        Assert.Null(agent.DeletedAt);
        Assert.Null(agent.RecycleBinExpiresAt);
    }

    [Fact]
    public void SoftDelete_PropertiesCanBeSet()
    {
        // Arrange
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent"
        };

        // Act
        agent.IsDeleted = true;
        agent.DeletedAt = DateTime.UtcNow;
        agent.IsInRecycleBin = true;
        agent.RecycleBinExpiresAt = DateTime.UtcNow.AddDays(30);

        // Assert
        Assert.True(agent.IsDeleted);
        Assert.NotNull(agent.DeletedAt);
        Assert.True(agent.IsInRecycleBin);
        Assert.NotNull(agent.RecycleBinExpiresAt);
    }

    [Fact]
    public void NavigationProperties_CanBeSet()
    {
        // Arrange
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        var personalityTemplate = new Core.Domain.Entities.PersonalityTemplateEntity
        {
            Id = Guid.NewGuid(),
            Name = "Professional"
        };

        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test Agent",
            UserId = user.Id,
            User = user,
            PersonalityTemplateId = personalityTemplate.Id,
            PersonalityTemplate = personalityTemplate
        };

        // Assert
        Assert.Equal(user, agent.User);
        Assert.Equal(personalityTemplate, agent.PersonalityTemplate);
    }
}
