using Application.Interfaces;
using Application.UseCases.Plugin.ListPluginCategories;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ListPluginCategoriesHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<ILogger<ListPluginCategoriesHandler>> _loggerMock;
    private readonly ListPluginCategoriesHandler _handler;

    public ListPluginCategoriesHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _loggerMock = new Mock<ILogger<ListPluginCategoriesHandler>>();
        _handler = new ListPluginCategoriesHandler(_pluginRepositoryMock.Object, _loggerMock.Object);
    }

    private static PluginEntity CreatePlugin(string name, string? category)
    {
        return new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsActive = true,
            IsPublic = true,
            PublishedAt = DateTime.UtcNow.AddDays(-5),
            Category = category,
            PriceCredits = 10,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_ReturnsDistinctCategories()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Plugin 1", "productivity"),
            CreatePlugin("Plugin 2", "analytics"),
            CreatePlugin("Plugin 3", "productivity"),
            CreatePlugin("Plugin 4", "security")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(3, result.Categories.Count);
        Assert.Contains(result.Categories, c => c.Name == "productivity");
        Assert.Contains(result.Categories, c => c.Name == "analytics");
        Assert.Contains(result.Categories, c => c.Name == "security");
    }

    [Fact]
    public async Task Handle_CountsPluginsPerCategory_Correctly()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Plugin 1", "productivity"),
            CreatePlugin("Plugin 2", "productivity"),
            CreatePlugin("Plugin 3", "productivity"),
            CreatePlugin("Plugin 4", "analytics"),
            CreatePlugin("Plugin 5", "analytics")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var productivity = result.Categories.First(c => c.Name == "productivity");
        var analytics = result.Categories.First(c => c.Name == "analytics");

        Assert.Equal(3, productivity.PluginCount);
        Assert.Equal(2, analytics.PluginCount);
    }

    [Fact]
    public async Task Handle_ExcludesPluginsWithoutCategory()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("With Category", "productivity"),
            CreatePlugin("No Category", null),
            CreatePlugin("Empty Category", "")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Categories);
        Assert.Equal("productivity", result.Categories.First().Name);
    }

    [Fact]
    public async Task Handle_ExcludesNonPublicPlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Public", "productivity"),
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Private",
                IsActive = true,
                IsPublic = false,
                PublishedAt = DateTime.UtcNow,
                Category = "private-category"
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Categories);
        Assert.Equal("productivity", result.Categories.First().Name);
    }

    [Fact]
    public async Task Handle_OrdersCategoriesAlphabetically()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Plugin 1", "zebra"),
            CreatePlugin("Plugin 2", "alpha"),
            CreatePlugin("Plugin 3", "middle")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("alpha", result.Categories[0].Name);
        Assert.Equal("middle", result.Categories[1].Name);
        Assert.Equal("zebra", result.Categories[2].Name);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoCategories()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("No Category 1", null),
            CreatePlugin("No Category 2", null)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Handle_ExcludesUnpublishedPlugins()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePlugin("Published", "productivity"),
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Unpublished",
                IsActive = true,
                IsPublic = true,
                PublishedAt = null,
                Category = "unpublished-category"
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPluginCategoriesQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Categories);
        Assert.Equal("productivity", result.Categories.First().Name);
    }
}
