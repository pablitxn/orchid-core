using Application.Interfaces;
using Application.UseCases.Marketplace.GetUserMarketplaceItems;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Marketplace;

public class GetUserMarketplaceItemsHandlerTests
{
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly Mock<IUserWorkflowRepository> _userWorkflowRepositoryMock;
    private readonly Mock<ILogger<GetUserMarketplaceItemsHandler>> _loggerMock;
    private readonly GetUserMarketplaceItemsHandler _handler;

    public GetUserMarketplaceItemsHandlerTests()
    {
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _userWorkflowRepositoryMock = new Mock<IUserWorkflowRepository>();
        _loggerMock = new Mock<ILogger<GetUserMarketplaceItemsHandler>>();
        _handler = new GetUserMarketplaceItemsHandler(
            _userPluginRepositoryMock.Object,
            _userWorkflowRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsUserMarketplaceItems_WithPluginsAndWorkflows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserMarketplaceItemsQuery(userId);

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PluginId = Guid.NewGuid(),
                IsActive = true,
                PurchasedAt = DateTime.UtcNow.AddDays(-10),
                Plugin = new PluginEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Plugin",
                    Description = "Plugin Description",
                    PriceCredits = 20
                }
            }
        };

        var userWorkflows = new List<UserWorkflowEntity>
        {
            new()
            {
                UserId = userId,
                WorkflowId = Guid.NewGuid(),
                PurchasedAt = DateTime.UtcNow.AddDays(-5),
                Workflow = new WorkflowEntity
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Workflow",
                    Description = "Workflow Description",
                    PriceCredits = 30,
                    Category = "Productivity"
                }
            }
        };

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        _userWorkflowRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWorkflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Plugins);
        Assert.Single(result.Workflows);
        Assert.Equal("Test Plugin", result.Plugins[0].Name);
        Assert.Equal("Test Workflow", result.Workflows[0].Name);
        Assert.Equal(50, result.TotalCreditsSpent); // 20 + 30
    }

    [Fact]
    public async Task Handle_ReturnsEmptyLists_WhenUserHasNoPurchases()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserMarketplaceItemsQuery(userId);

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserPluginEntity>());

        _userWorkflowRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserWorkflowEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Plugins);
        Assert.Empty(result.Workflows);
        Assert.Equal(0, result.TotalCreditsSpent);
    }

    [Fact]
    public async Task Handle_CalculatesTotalCreditsSpent_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserMarketplaceItemsQuery(userId);

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                UserId = userId,
                IsActive = true,
                PurchasedAt = DateTime.UtcNow,
                Plugin = new PluginEntity { Name = "Plugin 1", PriceCredits = 10 }
            },
            new()
            {
                UserId = userId,
                IsActive = true,
                PurchasedAt = DateTime.UtcNow,
                Plugin = new PluginEntity { Name = "Plugin 2", PriceCredits = 15 }
            }
        };

        var userWorkflows = new List<UserWorkflowEntity>
        {
            new()
            {
                UserId = userId,
                PurchasedAt = DateTime.UtcNow,
                Workflow = new WorkflowEntity { Name = "Workflow 1", PriceCredits = 25 }
            }
        };

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        _userWorkflowRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userWorkflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Equal(50, result.TotalCreditsSpent); // 10 + 15 + 25
    }

    [Fact]
    public async Task Handle_MapsPluginProperties_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var purchasedAt = DateTime.UtcNow.AddDays(-7);
        var query = new GetUserMarketplaceItemsQuery(userId);

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                UserId = userId,
                PluginId = pluginId,
                IsActive = true,
                PurchasedAt = purchasedAt,
                Plugin = new PluginEntity
                {
                    Id = pluginId,
                    Name = "Detailed Plugin",
                    Description = "Detailed Description",
                    PriceCredits = 35
                }
            }
        };

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        _userWorkflowRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserWorkflowEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        var plugin = result.Plugins[0];
        Assert.Equal(pluginId, plugin.Id);
        Assert.Equal("Detailed Plugin", plugin.Name);
        Assert.Equal("Detailed Description", plugin.Description);
        Assert.Equal(35, plugin.PriceCredits);
        Assert.True(plugin.IsActive);
        Assert.Equal(purchasedAt, plugin.PurchasedAt);
    }

    [Fact]
    public async Task Handle_ThrowsOperationFailedException_OnError()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserMarketplaceItemsQuery(userId);

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<OperationFailedException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsOnlyPlugins_WhenUserHasNoWorkflows()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserMarketplaceItemsQuery(userId);

        var userPlugins = new List<UserPluginEntity>
        {
            new()
            {
                UserId = userId,
                IsActive = true,
                PurchasedAt = DateTime.UtcNow,
                Plugin = new PluginEntity { Name = "Only Plugin", PriceCredits = 25 }
            }
        };

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userPlugins);

        _userWorkflowRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserWorkflowEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Plugins);
        Assert.Empty(result.Workflows);
        Assert.Equal(25, result.TotalCreditsSpent);
    }
}
