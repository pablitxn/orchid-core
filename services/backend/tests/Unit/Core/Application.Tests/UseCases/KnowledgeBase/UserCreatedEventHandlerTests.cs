using Application.Interfaces;
using Application.UseCases.KnowledgeBase.SeedDefaultFiles;
using Domain.Entities;
using Domain.Events;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.KnowledgeBase;

public class UserCreatedEventHandlerTests
{
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly Mock<IAgentRepository> _agentRepositoryMock;
    private readonly Mock<IUserPluginRepository> _userPluginRepositoryMock;
    private readonly Mock<ILogger<UserCreatedEventHandler>> _loggerMock;
    private readonly UserCreatedEventHandler _handler;

    public UserCreatedEventHandlerTests()
    {
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _agentRepositoryMock = new Mock<IAgentRepository>();
        _userPluginRepositoryMock = new Mock<IUserPluginRepository>();
        _loggerMock = new Mock<ILogger<UserCreatedEventHandler>>();
        _handler = new UserCreatedEventHandler(
            _subscriptionRepositoryMock.Object,
            _eventPublisherMock.Object,
            _agentRepositoryMock.Object,
            _userPluginRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_GrantsWelcomeCredits_WhenUserHasNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        SubscriptionEntity? createdSubscription = null;
        _subscriptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<SubscriptionEntity, CancellationToken>((s, _) => createdSubscription = s)
            .ReturnsAsync((SubscriptionEntity s, CancellationToken _) => s);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.NotNull(createdSubscription);
        Assert.Equal(10, createdSubscription.Credits); // WELCOME_CREDITS = 10
        Assert.Equal(userId, createdSubscription.UserId);
        _eventPublisherMock.Verify(
            x => x.PublishAsync(It.IsAny<SubscriptionCreatedEvent>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SkipsWelcomeCredits_WhenUserAlreadyHasSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        var existingSubscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = 100
        };

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSubscription);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _subscriptionRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_AssignsPluginsFromPublicAgents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId1 = Guid.NewGuid();
        var pluginId2 = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var publicAgents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 1",
                IsPublic = true,
                IsDeleted = false,
                PluginIds = new[] { pluginId1 }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Public Agent 2",
                IsPublic = true,
                IsDeleted = false,
                PluginIds = new[] { pluginId2 }
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Private Agent",
                IsPublic = false,
                IsDeleted = false,
                PluginIds = new[] { Guid.NewGuid() }
            }
        };

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(publicAgents);

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserAndPluginAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPluginEntity?)null);

        var createdUserPlugins = new List<UserPluginEntity>();
        _userPluginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserPluginEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UserPluginEntity, CancellationToken>((up, _) => createdUserPlugins.Add(up))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.Equal(2, createdUserPlugins.Count);
        Assert.Contains(createdUserPlugins, up => up.PluginId == pluginId1);
        Assert.Contains(createdUserPlugins, up => up.PluginId == pluginId2);
        Assert.All(createdUserPlugins, up => Assert.Equal(userId, up.UserId));
        Assert.All(createdUserPlugins, up => Assert.True(up.IsActive));
    }

    [Fact]
    public async Task Handle_SkipsExistingUserPlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingPluginId = Guid.NewGuid();
        var newPluginId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var publicAgents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = false,
                PluginIds = new[] { existingPluginId, newPluginId }
            }
        };

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(publicAgents);

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserAndPluginAsync(userId, existingPluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserPluginEntity { UserId = userId, PluginId = existingPluginId });

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserAndPluginAsync(userId, newPluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPluginEntity?)null);

        var createdUserPlugins = new List<UserPluginEntity>();
        _userPluginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserPluginEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UserPluginEntity, CancellationToken>((up, _) => createdUserPlugins.Add(up))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        Assert.Single(createdUserPlugins);
        Assert.Equal(newPluginId, createdUserPlugins[0].PluginId);
    }

    [Fact]
    public async Task Handle_ExcludesDeletedAgents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = true, // Deleted agent
                PluginIds = new[] { Guid.NewGuid() }
            }
        };

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _userPluginRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<UserPluginEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DoesNotThrow_WhenWelcomeCreditsGrantingFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        _subscriptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<SubscriptionEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AgentEntity>());

        // Act - Should not throw
        await _handler.Handle(notification, CancellationToken.None);

        // Assert - No exception thrown
    }

    [Fact]
    public async Task Handle_DoesNotThrow_WhenPluginAssignmentFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Agent repository error"));

        // Act - Should not throw
        await _handler.Handle(notification, CancellationToken.None);

        // Assert - No exception thrown
    }

    [Fact]
    public async Task Handle_HandlesAgentsWithNoPlugins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = false,
                PluginIds = Array.Empty<Guid>()
            },
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = false,
                PluginIds = null!
            }
        };

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _userPluginRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<UserPluginEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_DeduplicatesPluginsAcrossAgents()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedPluginId = Guid.NewGuid();
        var notification = new UserCreatedEvent(userId, "test@example.com");

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        var agents = new List<AgentEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = false,
                PluginIds = new[] { sharedPluginId }
            },
            new()
            {
                Id = Guid.NewGuid(),
                IsPublic = true,
                IsDeleted = false,
                PluginIds = new[] { sharedPluginId } // Same plugin
            }
        };

        _agentRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(agents);

        _userPluginRepositoryMock
            .Setup(x => x.GetByUserAndPluginAsync(userId, sharedPluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPluginEntity?)null);

        var createdUserPlugins = new List<UserPluginEntity>();
        _userPluginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserPluginEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UserPluginEntity, CancellationToken>((up, _) => createdUserPlugins.Add(up))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert - Should only create one user plugin (deduplicated)
        Assert.Single(createdUserPlugins);
    }
}
