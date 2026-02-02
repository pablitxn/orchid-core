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
    private readonly Mock<IUserCreditsRepository> _userCreditsRepositoryMock;
    private readonly Mock<ILogger<CreditTrackingService>> _loggerMock;
    private readonly CreditTrackingService _service;

    public CreditTrackingServiceTests()
    {
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _userCreditsRepositoryMock = new Mock<IUserCreditsRepository>();
        _loggerMock = new Mock<ILogger<CreditTrackingService>>();
        _service = new CreditTrackingService(
            _creditConsumptionRepositoryMock.Object,
            _userCreditsRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region TrackPluginUsageAsync Tests

    [Fact]
    public async Task TrackPluginUsageAsync_Should_CreateConsumptionRecord()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 1000);
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
    public async Task TrackPluginUsageAsync_Should_HandleNoUserCredits_WithZeroBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackPluginUsageAsync(userId, pluginId, "Test Plugin", 50);

        // Assert
        result.Should().NotBeNull();
        capturedEntity!.BalanceAfter.Should().Be(-50); // 0 - 50 = -50
    }

    [Fact]
    public async Task TrackPluginUsageAsync_Should_IncludeMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 1000);
        var metadata = "{\"operation\": \"search\"}";
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 500);
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
    public async Task TrackWorkflowUsageAsync_Should_HandleNoUserCredits_WithZeroBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackWorkflowUsageAsync(userId, workflowId, "Workflow", 100);

        // Assert
        result.Should().NotBeNull();
        capturedEntity!.BalanceAfter.Should().Be(-100);
    }

    [Fact]
    public async Task TrackWorkflowUsageAsync_Should_IncludeMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 500);
        var metadata = "{\"steps\": 5}";
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 200);
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
    public async Task TrackMessageCostAsync_Should_HandleNoUserCredits_WithZeroBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<CreditConsumptionEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackMessageCostAsync(userId, messageId, "Message", 10);

        // Assert
        result.Should().NotBeNull();
        capturedEntity!.BalanceAfter.Should().Be(-10);
    }

    [Fact]
    public async Task TrackMessageCostAsync_Should_SetConsumedAtTimestamp()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 200);
        CreditConsumptionEntity? capturedEntity = null;
        var beforeCall = DateTime.UtcNow;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 100);

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 100);

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 50);

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 100);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_ReturnFalse_When_NoUserCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditsEntity?)null);

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
        var userCredits = CreateUserCredits(userId, 0);

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 1000);
        var cts = new CancellationTokenSource();

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(userCredits);

        _creditConsumptionRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<CreditConsumptionEntity>(), cts.Token))
            .ReturnsAsync((CreditConsumptionEntity e, CancellationToken _) => e);

        // Act
        var result = await _service.TrackPluginUsageAsync(userId, pluginId, "Plugin", 50, cancellationToken: cts.Token);

        // Assert
        result.Should().NotBeNull();
        _userCreditsRepositoryMock.Verify(x => x.GetByUserIdAsync(userId, cts.Token), Times.Once);
    }

    [Fact]
    public async Task ValidateSufficientCreditsAsync_Should_SupportCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 100);
        var cts = new CancellationTokenSource();

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, cts.Token))
            .ReturnsAsync(userCredits);

        // Act
        var result = await _service.ValidateSufficientCreditsAsync(userId, 50, cts.Token);

        // Assert
        result.Should().BeTrue();
        _userCreditsRepositoryMock.Verify(x => x.GetByUserIdAsync(userId, cts.Token), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task TrackPluginUsageAsync_Should_HandleZeroCreditsConsumption()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var userCredits = CreateUserCredits(userId, 100);
        CreditConsumptionEntity? capturedEntity = null;

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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
        var userCredits = CreateUserCredits(userId, 1000);
        var capturedIds = new List<Guid>();

        _userCreditsRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userCredits);

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

    private static UserCreditsEntity CreateUserCredits(Guid userId, int credits)
    {
        return new UserCreditsEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AvailableCredits = credits,
            TotalCreditsGranted = credits,
            TotalCreditsConsumed = 0
        };
    }
}
