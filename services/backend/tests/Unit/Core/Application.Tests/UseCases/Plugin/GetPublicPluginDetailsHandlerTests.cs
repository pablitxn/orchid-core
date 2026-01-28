using Application.Interfaces;
using Application.UseCases.Plugin.ListAvailablePlugins;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class GetPublicPluginDetailsHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly GetPublicPluginDetailsHandler _handler;

    public GetPublicPluginDetailsHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _handler = new GetPublicPluginDetailsHandler(_pluginRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPluginDetails_WhenPluginExistsAndIsActive()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var query = new GetPublicPluginDetailsQuery(pluginId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            Description = "A test plugin",
            PriceCredits = 25,
            IsSubscriptionBased = false,
            IsActive = true,
            SourceUrl = "https://example.com/plugin",
            SystemName = "test_plugin",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(pluginId, result.Id);
        Assert.Equal("Test Plugin", result.Name);
        Assert.Equal(25, result.PriceCredits);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenPluginNotFound()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var query = new GetPublicPluginDetailsQuery(pluginId);

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenPluginIsInactive()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var query = new GetPublicPluginDetailsQuery(pluginId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Inactive Plugin",
            Description = "An inactive plugin",
            IsActive = false
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_ReturnsSubscriptionBasedPlugin_Correctly()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var query = new GetPublicPluginDetailsQuery(pluginId);

        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Premium Plugin",
            Description = "A subscription-based plugin",
            PriceCredits = 0,
            IsSubscriptionBased = true,
            IsActive = true
        };

        _pluginRepositoryMock
            .Setup(x => x.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsSubscriptionBased);
    }
}
