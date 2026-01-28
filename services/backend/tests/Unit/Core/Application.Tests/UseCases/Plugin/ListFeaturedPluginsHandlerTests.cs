using Application.Interfaces;
using Application.UseCases.Plugin.ListFeaturedPlugins;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ListFeaturedPluginsHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<ILogger<ListFeaturedPluginsHandler>> _loggerMock;
    private readonly ListFeaturedPluginsHandler _handler;

    public ListFeaturedPluginsHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _loggerMock = new Mock<ILogger<ListFeaturedPluginsHandler>>();
        _handler = new ListFeaturedPluginsHandler(_pluginRepositoryMock.Object, _loggerMock.Object);
    }

    private static PluginEntity CreatePlugin(
        string name,
        bool isFeatured,
        double rating = 4.0,
        int installCount = 100,
        bool isPublic = true,
        bool isActive = true)
    {
        return new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            IsActive = isActive,
            IsPublic = isPublic,
            IsFeatured = isFeatured,
            PublishedAt = DateTime.UtcNow.AddDays(-5),
            Rating = rating,
            InstallCount = installCount,
            PriceCredits = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_ReturnsOnlyFeaturedPlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Featured 1", isFeatured: true),
            CreatePlugin("Not Featured", isFeatured: false),
            CreatePlugin("Featured 2", isFeatured: true)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Plugins.Count);
        Assert.All(result.Plugins, p => Assert.True(p.IsFeatured));
    }

    [Fact]
    public async Task Handle_RespectsLimitParameter()
    {
        // Arrange
        var plugins = Enumerable.Range(1, 20)
            .Select(i => CreatePlugin($"Featured {i}", isFeatured: true))
            .ToList();

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery(Limit: 5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(5, result.Plugins.Count);
    }

    [Fact]
    public async Task Handle_OrdersByRatingThenInstallCount()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Low Rating", isFeatured: true, rating: 3.0, installCount: 500),
            CreatePlugin("High Rating Low Installs", isFeatured: true, rating: 5.0, installCount: 50),
            CreatePlugin("High Rating High Installs", isFeatured: true, rating: 5.0, installCount: 200),
            CreatePlugin("Medium Rating", isFeatured: true, rating: 4.0, installCount: 100)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("High Rating High Installs", result.Plugins[0].Name);
        Assert.Equal("High Rating Low Installs", result.Plugins[1].Name);
        Assert.Equal("Medium Rating", result.Plugins[2].Name);
        Assert.Equal("Low Rating", result.Plugins[3].Name);
    }

    [Fact]
    public async Task Handle_ExcludesNonPublicPlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Public Featured", isFeatured: true, isPublic: true),
            CreatePlugin("Private Featured", isFeatured: true, isPublic: false)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins);
        Assert.Equal("Public Featured", result.Plugins.First().Name);
    }

    [Fact]
    public async Task Handle_ExcludesInactivePlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Active Featured", isFeatured: true, isActive: true),
            CreatePlugin("Inactive Featured", isFeatured: true, isActive: false)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins);
        Assert.Equal("Active Featured", result.Plugins.First().Name);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoFeaturedPlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Not Featured 1", isFeatured: false),
            CreatePlugin("Not Featured 2", isFeatured: false)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Plugins);
    }

    [Fact]
    public async Task Handle_DefaultLimitIs10()
    {
        // Arrange
        var plugins = Enumerable.Range(1, 15)
            .Select(i => CreatePlugin($"Featured {i}", isFeatured: true))
            .ToList();

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListFeaturedPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(10, result.Plugins.Count);
    }
}
