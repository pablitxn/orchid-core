using Core.Domain.Entities;
using Domain.Entities;

namespace Domain.Tests.Entities;

public class LoginHistoryEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;

        var loginHistory = new LoginHistoryEntity
        {
            Id = id,
            UserId = userId,
            Timestamp = timestamp,
            IpAddress = "192.168.1.1",
            UserAgent = "Mozilla/5.0",
            Location = "New York, US",
            Success = true,
            FailureReason = null
        };

        // Assert
        Assert.Equal(id, loginHistory.Id);
        Assert.Equal(userId, loginHistory.UserId);
        Assert.Equal(timestamp, loginHistory.Timestamp);
        Assert.Equal("192.168.1.1", loginHistory.IpAddress);
        Assert.Equal("Mozilla/5.0", loginHistory.UserAgent);
        Assert.Equal("New York, US", loginHistory.Location);
        Assert.True(loginHistory.Success);
        Assert.Null(loginHistory.FailureReason);
    }

    [Fact]
    public void FailedLogin_CanBeRecorded()
    {
        // Arrange
        var loginHistory = new LoginHistoryEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            IpAddress = "10.0.0.1",
            Success = false,
            FailureReason = "Invalid password"
        };

        // Assert
        Assert.False(loginHistory.Success);
        Assert.Equal("Invalid password", loginHistory.FailureReason);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var loginHistory = new LoginHistoryEntity();

        // Assert
        Assert.Equal(string.Empty, loginHistory.IpAddress);
        Assert.Null(loginHistory.UserAgent);
        Assert.Null(loginHistory.Location);
        Assert.Null(loginHistory.FailureReason);
    }

    [Fact]
    public void User_NavigationProperty_CanBeSet()
    {
        // Arrange
        var user = new UserEntity { Id = Guid.NewGuid(), Email = "test@example.com" };
        var loginHistory = new LoginHistoryEntity
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user
        };

        // Assert
        Assert.Equal(user, loginHistory.User);
    }
}
