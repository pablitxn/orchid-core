using Application.Interfaces;
using Application.UseCases.Plugin.ListAvailablePlugins;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ListAvailablePluginsHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly ListAvailablePluginsHandler _handler;

    public ListAvailablePluginsHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _handler = new ListAvailablePluginsHandler(
            _pluginRepositoryMock.Object,
            _userPluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsAvailablePlugins_WithOwnershipStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var ownedPluginId = Guid.NewGuid();
        var notOwnedPluginId = Guid.NewGuid();
        var query = new ListAvailablePluginsQuery(userId);

        var allPlugins = new List<PluginEntity>
        {
            new()
            {
                Id = ownedPluginId,
                Name = "Owned Plugin",
                Description = "A plugin the user owns",
                PriceCredits = 20,
                IsSubscriptionBased = false,
                IsActive = true
            },
            new()
            {
                Id = notOwnedPluginId,
                Name = "Not Owned Plugin",
                Description = "A plugin the user doesn't own",
                PriceCredits = 30,
                IsSubscriptionBased = false,
                IsActive = true
            }
        };

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = ownedPluginId,
                IsActive = true
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPlugins);

        _userPluginRepositoryMock
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Plugins.Count());

        var ownedPlugin = result.Plugins.First(p => p.Id == ownedPluginId);
        Assert.True(ownedPlugin.UserOwns);

        var notOwnedPlugin = result.Plugins.First(p => p.Id == notOwnedPluginId);
        Assert.False(notOwnedPlugin.UserOwns);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoActivePlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new ListAvailablePluginsQuery(userId);

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        _userPluginRepositoryMock
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserPluginEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Plugins);
    }

    [Fact]
    public async Task Handle_IgnoresInactiveUserPlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var query = new ListAvailablePluginsQuery(userId);

        var allPlugins = new List<PluginEntity>
        {
            new()
            {
                Id = pluginId,
                Name = "Test Plugin",
                Description = "Test",
                PriceCredits = 10,
                IsActive = true
            }
        };

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = pluginId,
                IsActive = false // Inactive user plugin
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(allPlugins);

        _userPluginRepositoryMock
            .Setup(x => x.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        var plugin = result.Plugins.First();
        Assert.False(plugin.UserOwns); // Should be false since user plugin is inactive
    }
}
