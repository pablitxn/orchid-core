using Domain.Entities;

namespace Domain.Tests.Entities;

public class PluginEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var plugin = new PluginEntity
        {
            Id = id,
            Name = "Test Plugin",
            Description = "A test plugin",
            SourceUrl = "https://example.com/plugin",
            IsActive = true,
            PriceCredits = 25,
            IsSubscriptionBased = true,
            SystemName = "test_plugin"
        };

        // Assert
        Assert.Equal(id, plugin.Id);
        Assert.Equal("Test Plugin", plugin.Name);
        Assert.Equal("A test plugin", plugin.Description);
        Assert.Equal("https://example.com/plugin", plugin.SourceUrl);
        Assert.True(plugin.IsActive);
        Assert.Equal(25, plugin.PriceCredits);
        Assert.True(plugin.IsSubscriptionBased);
        Assert.Equal("test_plugin", plugin.SystemName);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var plugin = new PluginEntity();

        // Assert
        Assert.Equal(string.Empty, plugin.Name);
        Assert.False(plugin.IsActive);
        Assert.Equal(10, plugin.PriceCredits);
        Assert.False(plugin.IsSubscriptionBased);
        Assert.Null(plugin.SystemName);
    }
}
