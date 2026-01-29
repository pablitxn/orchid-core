using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRealtimeNotificationPort> _realtimeNotificationPortMock;
    private readonly Mock<INotificationPreferencesRepository> _preferencesRepositoryMock;
    private readonly Mock<IMatrixBridgePort> _matrixBridgePortMock;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _realtimeNotificationPortMock = new Mock<IRealtimeNotificationPort>();
        _preferencesRepositoryMock = new Mock<INotificationPreferencesRepository>();
        _matrixBridgePortMock = new Mock<IMatrixBridgePort>();
        _loggerMock = new Mock<ILogger<NotificationService>>();

        _service = new NotificationService(
            _notificationRepositoryMock.Object,
            _userRepositoryMock.Object,
            _realtimeNotificationPortMock.Object,
            _preferencesRepositoryMock.Object,
            _matrixBridgePortMock.Object,
            _loggerMock.Object);
    }

    #region SendLowCreditAlertAsync Tests

    [Fact]
    public async Task SendLowCreditAlertAsync_Should_CreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId, Email = "test@example.com" };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendLowCreditAlertAsync(userId, 50, 100);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.UserId.Should().Be(userId);
        capturedNotification.Type.Should().Be(NotificationType.LowCreditAlert.ToString());
        capturedNotification.Title.Should().Be("Low Credit Balance Alert");
        capturedNotification.Message.Should().Contain("50 credits remaining");
        capturedNotification.Message.Should().Contain("100 credits");
        capturedNotification.ActionUrl.Should().Be("/credits");
        capturedNotification.Priority.Should().Be(2);
    }

    [Fact]
    public async Task SendLowCreditAlertAsync_Should_SendRealtimeNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendLowCreditAlertAsync(userId, 50, 100);

        // Assert
        _realtimeNotificationPortMock.Verify(
            x => x.SendNotificationAsync(userId, It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendLowCreditAlertAsync_Should_NotSendNotification_When_UserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserEntity?)null);

        // Act
        await _service.SendLowCreditAlertAsync(userId, 50, 100);

        // Assert
        _notificationRepositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region SendCreditExhaustedAlertAsync Tests

    [Fact]
    public async Task SendCreditExhaustedAlertAsync_Should_CreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendCreditExhaustedAlertAsync(userId);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Type.Should().Be(NotificationType.CreditExhausted.ToString());
        capturedNotification.Title.Should().Be("Credits Exhausted");
        capturedNotification.Priority.Should().Be(3);
    }

    #endregion

    #region SendCreditAddedNotificationAsync Tests

    [Fact]
    public async Task SendCreditAddedNotificationAsync_Should_CreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendCreditAddedNotificationAsync(userId, 500, 1500);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Type.Should().Be(NotificationType.CreditAdded.ToString());
        capturedNotification.Message.Should().Contain("500 credits");
        capturedNotification.Message.Should().Contain("1500 credits");
        capturedNotification.Priority.Should().Be(1);
    }

    #endregion

    #region SendPurchaseConfirmationAsync Tests

    [Fact]
    public async Task SendPurchaseConfirmationAsync_Should_CreateNotification_ForPlugin()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendPurchaseConfirmationAsync(userId, "Plugin", "Web Search", 50);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Type.Should().Be(NotificationType.PurchaseConfirmation.ToString());
        capturedNotification.Title.Should().Contain("Plugin Purchase Confirmed");
        capturedNotification.Message.Should().Contain("Web Search");
        capturedNotification.Message.Should().Contain("50 credits");
        capturedNotification.ActionUrl.Should().Be("/plugins");
    }

    [Fact]
    public async Task SendPurchaseConfirmationAsync_Should_CreateNotification_ForWorkflow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendPurchaseConfirmationAsync(userId, "Workflow", "Data Pipeline", 100);

        // Assert
        capturedNotification!.ActionUrl.Should().Be("/workflows");
    }

    #endregion

    #region SendSubscriptionExpiringAlertAsync Tests

    [Fact]
    public async Task SendSubscriptionExpiringAlertAsync_Should_CreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        var expirationDate = DateTime.UtcNow.AddDays(7);
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendSubscriptionExpiringAlertAsync(userId, expirationDate);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Type.Should().Be(NotificationType.SubscriptionExpiring.ToString());
        capturedNotification.Title.Should().Be("Subscription Expiring Soon");
        // The days calculation might be 6 or 7 depending on exact timing
        capturedNotification.Message.Should().Contain("days");
        capturedNotification.ActionUrl.Should().Be("/subscription");
        capturedNotification.Priority.Should().Be(2);
    }

    #endregion

    #region SendSubscriptionExpiredAlertAsync Tests

    [Fact]
    public async Task SendSubscriptionExpiredAlertAsync_Should_CreateNotification()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendSubscriptionExpiredAlertAsync(userId);

        // Assert
        capturedNotification.Should().NotBeNull();
        capturedNotification!.Type.Should().Be(NotificationType.SubscriptionExpired.ToString());
        capturedNotification.Title.Should().Be("Subscription Expired");
        capturedNotification.ActionText.Should().Be("Renew Now");
        capturedNotification.Priority.Should().Be(3);
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public async Task SendLowCreditAlertAsync_Should_IncludeMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        NotificationEntity? capturedNotification = null;

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationEntity, CancellationToken>((n, _) => capturedNotification = n)
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendLowCreditAlertAsync(userId, 50, 100);

        // Assert
        capturedNotification!.Metadata.Should().Contain("50");
        capturedNotification.Metadata.Should().Contain("100");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task SendLowCreditAlertAsync_Should_SupportCancellation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserEntity { Id = userId };
        var cts = new CancellationTokenSource();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId, cts.Token))
            .ReturnsAsync(user);

        _notificationRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<NotificationEntity>(), cts.Token))
            .ReturnsAsync((NotificationEntity n, CancellationToken _) => n);

        // Act
        await _service.SendLowCreditAlertAsync(userId, 50, 100, cts.Token);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByIdAsync(userId, cts.Token), Times.Once);
    }

    #endregion
}
