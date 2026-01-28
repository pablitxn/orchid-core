using Application.Interfaces;
using Application.UseCases.Dashboard.GetSecurityMetrics;
using Core.Application.Interfaces;
using Core.Application.UseCases.Dashboard.GetSecurityMetrics;
using Core.Domain.Entities;
using Domain.Entities;
using Domain.Exceptions;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Dashboard;

public class GetSecurityMetricsHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILoginHistoryRepository> _loginHistoryRepositoryMock;
    private readonly GetSecurityMetricsHandler _handler;

    public GetSecurityMetricsHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _loginHistoryRepositoryMock = new Mock<ILoginHistoryRepository>();
        _handler = new GetSecurityMetricsHandler(
            _userRepositoryMock.Object,
            _loginHistoryRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSecurityMetrics_WhenUserExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSecurityMetricsQuery(userId);
        var passwordChangedAt = DateTime.UtcNow.AddDays(-10);

        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            PasswordChangedAt = passwordChangedAt
        };

        var loginHistory = new List<LoginHistoryEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddHours(-1),
                IpAddress = "192.168.1.1",
                UserAgent = "Chrome/120",
                Location = "New York",
                Success = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                IpAddress = "192.168.1.2",
                UserAgent = "Firefox/115",
                Location = "Boston",
                Success = false,
                FailureReason = "Invalid password"
            }
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginHistory);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetFailedAttemptsCountAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.RecentLogins.Count);
        Assert.Equal(3, result.FailedLoginAttempts);
        Assert.Equal(passwordChangedAt, result.LastPasswordChange);
        Assert.NotNull(result.LastLogin);
    }

    [Fact]
    public async Task Handle_ThrowsUserNotFoundException_WhenUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSecurityMetricsQuery(userId);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((UserEntity?)null);

        // Act & Assert
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            _handler.Handle(query, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ReturnsActiveDevices_FromSuccessfulLogins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSecurityMetricsQuery(userId);

        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com"
        };

        var loginHistory = new List<LoginHistoryEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1",
                UserAgent = "Chrome/120",
                Success = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddHours(-1),
                IpAddress = "192.168.1.2",
                UserAgent = "Firefox/115",
                Success = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                IpAddress = "192.168.1.3",
                UserAgent = "Safari/17",
                Success = false // Failed login - should not appear in active devices
            }
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginHistory);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetFailedAttemptsCountAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.ActiveDevices.Count);
        Assert.Contains("Chrome/120", result.ActiveDevices);
        Assert.Contains("Firefox/115", result.ActiveDevices);
        Assert.DoesNotContain("Safari/17", result.ActiveDevices);
    }

    [Fact]
    public async Task Handle_ReturnsNullLastLogin_WhenNoSuccessfulLogins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetSecurityMetricsQuery(userId);

        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com"
        };

        var loginHistory = new List<LoginHistoryEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Timestamp = DateTime.UtcNow,
                IpAddress = "192.168.1.1",
                Success = false
            }
        };

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetRecentByUserIdAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginHistory);

        _loginHistoryRepositoryMock
            .Setup(x => x.GetFailedAttemptsCountAsync(userId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Null(result.LastLogin);
    }
}
