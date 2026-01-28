using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class CreditTrackingServiceTests
{
    private readonly Mock<ICreditConsumptionRepository> _creditConsumptionRepositoryMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<ILogger<CreditTrackingService>> _loggerMock;
    private readonly CreditTrackingService _service;

    public CreditTrackingServiceTests()
    {
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _loggerMock = new Mock<ILogger<CreditTrackingService>>();
        _service = new CreditTrackingService(
            _creditConsumptionRepositoryMock.Object,
            _subscriptionRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region TrackPluginUsageAsync Tests

    [Fact]
    public async Task TrackPluginUsageAsync_Should_CreateConsumptionRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 1000);
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackPluginUsageAsync(userId, pluginId, "Test Plugin", 50);

        // Assert
        result.Should().NotBeNull();
        capturedEntity.Should().NotBeNull();
        capturedEntity!.ConsumptionType.Should().Be("plugin");
        capturedEntity.ResourceId.Should().Be(pluginId);
        capturedEntity.ResourceName.Should().Be("Test Plugin");
        capturedEntity.CreditsConsumed.Should().Be(50);
        capturedEntity.BalanceAfter.Should().Be(950);
    }

    [Fact]
    public async Task TrackPluginUsageAsync_Should_ThrowException_When_NoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var act = () => _service.TrackPluginUsageAsync(userId, pluginId, "Test Plugin", 50);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active subscription*");
    }

    [Fact]
    public async Task TrackPluginUsageAsync_Should_IncludeMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 1000);
        var metadata = "{\"operation\": \"search\"}";
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        await _service.TrackPluginUsageAsync(userId, pluginId, "Test Plugin", 50, metadata);

        // Assert
        capturedEntity!.Metadata.Should().Be(metadata);
    }

    #endregion

    #region TrackWorkflowUsageAsync Tests

    [Fact]
    public async Task TrackWorkflowUsageAsync_Should_CreateConsumptionRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 500);
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackWorkflowUsageAsync(userId, workflowId, "Data Pipeline", 100);

        // Assert
        result.Should().NotBeNull();
        capturedEntity.Should().NotBeNull();
        capturedEntity!.ConsumptionType.Should().Be("workflow");
        capturedEntity.ResourceId.Should().Be(workflowId);
        capturedEntity.ResourceName.Should().Be("Data Pipeline");
        capturedEntity.CreditsConsumed.Should().Be(100);
        capturedEntity.BalanceAfter.Should().Be(400);
    }

    [Fact]
    public async Task TrackWorkflowUsageAsync_Should_ThrowException_When_NoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var act = () => _service.TrackWorkflowUsageAsync(userId, workflowId, "Workflow", 100);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active subscription*");
    }

    [Fact]
    public async Task TrackWorkflowUsageAsync_Should_IncludeMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 500);
        var metadata = "{\"steps\": 5}";
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        await _service.TrackWorkflowUsageAsync(userId, workflowId, "Workflow", 100, metadata);

        // Assert
        capturedEntity!.Metadata.Should().Be(metadata);
    }

    #endregion

    #region TrackMessageCostAsync Tests

    [Fact]
    public async Task TrackMessageCostAsync_Should_CreateConsumptionRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 200);
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackMessageCostAsync(userId, messageId, "Chat message", 10);

        // Assert
        result.Should().NotBeNull();
        capturedEntity.Should().NotBeNull();
        capturedEntity!.ConsumptionType.Should().Be("message");
        capturedEntity.ResourceId.Should().Be(messageId);
        capturedEntity.ResourceName.Should().Be("Chat message");
        capturedEntity.CreditsConsumed.Should().Be(10);
        capturedEntity.BalanceAfter.Should().Be(190);
    }

    [Fact]
    public async Task TrackMessageCostAsync_Should_ThrowException_When_NoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var act = () => _service.TrackMessageCostAsync(userId, messageId, "Message", 10);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no active subscription*");
    }

    [Fact]
    public async Task TrackMessageCostAsync_Should_SetConsumedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 200);
        CreditConsumptionEntity? capturedEntity = null;
        var beforeCall = DateTime.UtcNow;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        await _service.TrackMessageCostAsync(userId, messageId, "Message", 10);

        // Assert
        capturedEntity!.ConsumedAt.Should().BeOnOrAfter(beforeCall);
        capturedEntity.ConsumedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region ValidateSufficientCreditsAsync Tests

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnTrue_When_SufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 100);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 50);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnTrue_When_ExactCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 100);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 100);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnFalse_When_InsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 50);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 100);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnFalse_When_NoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 10);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnFalse_When_ZeroCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 0);

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 1);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task TrackPluginUsageAsync_Should_SupportCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 1000);
        var cts = new CancellationTokenSource();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), cts.Token))
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackPluginUsageAsync(userId, pluginId, "Plugin", 50, cancellationToken: cts.Token);

        // Assert
        result.Should().NotBeNull();
        _subscriptionRepositoryMock.Verify(x => x.GetByUserIdAsync(userId, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_SupportCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 100);
        var cts = new CancellationTokenSource();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(subscription);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 50, cts.Token);

        // Assert
        result.Should().BeTrue();
        _subscriptionRepositoryMock.Verify(x => x.GetByUserIdAsync(userId, cts.Token), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TrackPluginUsageAsync_Should_HandleZeroCreditsConsumption()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 100);
        CreditConsumptionEntity? capturedEntity = null;

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackPluginUsageAsync(userId, pluginId, "Free Plugin", 0);

        // Assert
        capturedEntity!.CreditsConsumed.Should().Be(0);
        capturedEntity.BalanceAfter.Should().Be(100);
    }

    [Fact]
    public async Task TrackPluginUsageAsync_Should_GenerateUniqueIds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var subscription = CreateSubscription(userId, 1000);
        var capturedIds = new List<Guid>();

        _subscriptionRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedIds.Add(e.Id))
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        await _service.TrackPluginUsageAsync(userId, pluginId, "Plugin", 10);
        await _service.TrackPluginUsageAsync(userId, pluginId, "Plugin", 10);
        await _service.TrackPluginUsageAsync(userId, pluginId, "Plugin", 10);

        // Assert
        capturedIds.Should().HaveCount(3);
        capturedIds.Distinct().Should().HaveCount(3);
    }

    #endregion

    private static SubscriptionEntity CreateSubscription(Guid userId, int credits)
    {
        return new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = credits
        };
    }
}
