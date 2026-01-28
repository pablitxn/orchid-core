using Application.Interfaces;
using Application.UseCases.Plugin.ListPublicPlugins;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Plugin;

public class ListPublicPluginsHandlerTests
{
    private readonly Mock<IPluginRepository> _pluginRepositoryMock;
    private readonly Mock<ILogger<ListPublicPluginsHandler>> _loggerMock;
    private readonly ListPublicPluginsHandler _handler;

    public ListPublicPluginsHandlerTests()
    {
        _pluginRepositoryMock = new Mock<IPluginRepository>();
        _loggerMock = new Mock<ILogger<ListPublicPluginsHandler>>();
        _handler = new ListPublicPluginsHandler(_pluginRepositoryMock.Object, _loggerMock.Object);
    }

    private static PluginEntity CreatePublicPlugin(
        string name = "Test Plugin",
        string? category = "productivity",
        double rating = 4.5,
        int installCount = 100,
        bool isFeatured = false,
        int priceCredits = 10)
    {
        return new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = $"Description for {name}",
            PriceCredits = priceCredits,
            IsSubscriptionBased = false,
            IsActive = true,
            IsPublic = true,
            PublishedAt = DateTime.UtcNow.AddDays(-5),
            Category = category,
            Rating = rating,
            InstallCount = installCount,
            IsFeatured = isFeatured,
            Tags = "test,sample",
            Author = "Test Author",
            Version = "1.0.0",
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        };
    }

    [Fact]
    public async Task Handle_ReturnsOnlyPublicActivePublishedPlugins()
    {
        // Arrange
        var query = new ListPublicPluginsQuery();

        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Public Plugin"),
            new() { Id = Guid.NewGuid(), Name = "Not Public", IsActive = true, IsPublic = false, PublishedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Not Active", IsActive = false, IsPublic = true, PublishedAt = DateTime.UtcNow },
            new() { Id = Guid.NewGuid(), Name = "Not Published", IsActive = true, IsPublic = true, PublishedAt = null }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Plugins.Items);
        Assert.Equal("Public Plugin", result.Plugins.Items.First().Name);
    }

    [Fact]
    public async Task Handle_AppliesPagination_Correctly()
    {
        // Arrange
        var plugins = Enumerable.Range(1, 50)
            .Select(i => CreatePublicPlugin($"Plugin {i}"))
            .ToList();

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { Page = 2, PageSize = 10 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(50, result.Plugins.TotalCount);
        Assert.Equal(10, result.Plugins.Items.Count);
        Assert.Equal(2, result.Plugins.Page);
        Assert.Equal(10, result.Plugins.PageSize);
        Assert.Equal(5, result.Plugins.TotalPages);
        Assert.True(result.Plugins.HasPreviousPage);
        Assert.True(result.Plugins.HasNextPage);
    }

    [Fact]
    public async Task Handle_FiltersBy_SearchTerm()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Knowledge Search Pro"),
            CreatePublicPlugin("Image Analyzer"),
            CreatePublicPlugin("Code Assistant")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { SearchTerm = "search" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins.Items);
        Assert.Equal("Knowledge Search Pro", result.Plugins.Items.First().Name);
    }

    [Fact]
    public async Task Handle_FiltersBy_Category()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Plugin A", category: "productivity"),
            CreatePublicPlugin("Plugin B", category: "analytics"),
            CreatePublicPlugin("Plugin C", category: "productivity")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { Category = "productivity" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Plugins.Items.Count);
        Assert.All(result.Plugins.Items, p => Assert.Equal("productivity", p.Category));
    }

    [Fact]
    public async Task Handle_FiltersBy_MinRating()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("High Rated", rating: 4.8),
            CreatePublicPlugin("Medium Rated", rating: 3.5),
            CreatePublicPlugin("Low Rated", rating: 2.0)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { MinRating = 4.0m };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins.Items);
        Assert.Equal("High Rated", result.Plugins.Items.First().Name);
    }

    [Fact]
    public async Task Handle_FiltersBy_PriceRange()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Cheap", priceCredits: 5),
            CreatePublicPlugin("Medium", priceCredits: 15),
            CreatePublicPlugin("Expensive", priceCredits: 50)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { MinPrice = 10, MaxPrice = 20 };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins.Items);
        Assert.Equal("Medium", result.Plugins.Items.First().Name);
    }

    [Fact]
    public async Task Handle_SortsBy_Rating_Descending()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Low", rating: 2.0),
            CreatePublicPlugin("High", rating: 5.0),
            CreatePublicPlugin("Medium", rating: 3.5)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { SortBy = PluginSortBy.Rating, SortDescending = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("High", result.Plugins.Items[0].Name);
        Assert.Equal("Medium", result.Plugins.Items[1].Name);
        Assert.Equal("Low", result.Plugins.Items[2].Name);
    }

    [Fact]
    public async Task Handle_SortsBy_Popularity()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Less Popular", installCount: 50),
            CreatePublicPlugin("Most Popular", installCount: 1000),
            CreatePublicPlugin("Moderately Popular", installCount: 200)
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { SortBy = PluginSortBy.Popularity, SortDescending = true };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Most Popular", result.Plugins.Items[0].Name);
        Assert.Equal("Moderately Popular", result.Plugins.Items[1].Name);
        Assert.Equal("Less Popular", result.Plugins.Items[2].Name);
    }

    [Fact]
    public async Task Handle_SortsBy_Name_Ascending()
    {
        // Arrange
        var plugins = new List<PluginEntity>
        {
            CreatePublicPlugin("Charlie"),
            CreatePublicPlugin("Alpha"),
            CreatePublicPlugin("Bravo")
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery { SortBy = PluginSortBy.Name, SortDescending = false };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal("Alpha", result.Plugins.Items[0].Name);
        Assert.Equal("Bravo", result.Plugins.Items[1].Name);
        Assert.Equal("Charlie", result.Plugins.Items[2].Name);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoPluginsMatch()
    {
        // Arrange
        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var query = new ListPublicPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Plugins.Items);
        Assert.Equal(0, result.Plugins.TotalCount);
    }

    [Fact]
    public async Task Handle_MapsAllPluginProperties_Correctly()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow;
        var publishedAt = DateTime.UtcNow.AddDays(-5);

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
                IsPublic = true,
                PublishedAt = publishedAt,
                SourceUrl = "https://example.com/test",
                Author = "John Doe",
                Category = "productivity",
                Icon = "icon.png",
                Tags = "test,sample",
                Rating = 4.5,
                ReviewCount = 100,
                InstallCount = 500,
                IsFeatured = true,
                Version = "2.0.0",
                Compatibility = "all",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            }
        };

        _pluginRepositoryMock
            .Setup(x => x.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var query = new ListPublicPluginsQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var plugin = result.Plugins.Items.First();
        Assert.Equal(pluginId, plugin.Id);
        Assert.Equal("Test Plugin", plugin.Name);
        Assert.Equal("Test Description", plugin.Description);
        Assert.Equal(25, plugin.PriceCredits);
        Assert.True(plugin.IsSubscriptionBased);
        Assert.True(plugin.IsActive);
        Assert.Equal("https://example.com/test", plugin.SourceUrl);
        Assert.Equal("John Doe", plugin.Author);
        Assert.Equal("productivity", plugin.Category);
        Assert.Equal("icon.png", plugin.Icon);
        Assert.Equal("test,sample", plugin.Tags);
        Assert.Equal(4.5, plugin.Rating);
        Assert.Equal(100, plugin.ReviewCount);
        Assert.Equal(500, plugin.InstallCount);
        Assert.True(plugin.IsFeatured);
        Assert.Equal("2.0.0", plugin.Version);
        Assert.Equal("all", plugin.Compatibility);
        Assert.Equal(createdAt, plugin.CreatedAt);
        Assert.Equal(updatedAt, plugin.UpdatedAt);
        Assert.Equal(publishedAt, plugin.PublishedAt);
    }
}
