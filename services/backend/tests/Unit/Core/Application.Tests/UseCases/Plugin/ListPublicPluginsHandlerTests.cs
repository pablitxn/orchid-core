using Application.Interfaces;
using Application.UseCases.Plugin.ListPublicPlugins;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ListPublicPluginsHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly ListPublicPluginsHandler _handler;

    public ListPublicPluginsHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _handler = new ListPublicPluginsHandler(_pluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAllActivePlugins()
    {
        // Arrange
        var query = new ListPublicPluginsQuery();

        var plugins = new List<PluginEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Plugin A",
                Description = "Description A",
                PriceCredits = 10,
                IsSubscriptionBased = false,
                IsActive = true,
                SourceUrl = "https://example.com/a",
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Plugin B",
                Description = "Description B",
                PriceCredits = 20,
                IsSubscriptionBased = true,
                IsActive = true,
                SourceUrl = "https://example.com/b",
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Plugins.Count());
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoActivePlugins()
    {
        // Arrange
        var query = new ListPublicPluginsQuery();

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Plugins);
    }

    [Fact]
    public async Task Handle_MapsPluginProperties_Correctly()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var query = new ListPublicPluginsQuery();

        var plugins = new List<PluginEntity>
        {
            new()
            {
                Id = pluginId,
                Name = "Test Plugin",
                Description = "Test Description",
                PriceCredits = 25,
                IsSubscriptionBased = true,
                IsActive = true,
                SourceUrl = "https://example.com/test",
                CreatedAt = createdAt
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var plugin = result.Plugins.First();
        Assert.Equal(pluginId, plugin.Id);
        Assert.Equal("Test Plugin", plugin.Name);
        Assert.Equal("Test Description", plugin.Description);
        Assert.Equal(25, plugin.PriceCredits);
        Assert.True(plugin.IsSubscriptionBased);
        Assert.True(plugin.IsActive);
        Assert.Equal("https://example.com/test", plugin.SourceUrl);
        Assert.Equal(createdAt, plugin.CreatedAt);
    }
}
