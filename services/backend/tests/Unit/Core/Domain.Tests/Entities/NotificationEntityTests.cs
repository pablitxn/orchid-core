using Domain.Entities;

namespace Domain.Tests.Entities;

public class NotificationEntityTests
{
    [Fact]
    public void MarkAsRead_WhenNotRead_SetsIsReadAndReadAt()
    {
        // Arrange
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test message",
            IsRead = false
        };

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public void MarkAsRead_WhenAlreadyRead_DoesNotChangeReadAt()
    {
        // Arrange
        var originalReadAt = DateTime.UtcNow.AddMinutes(-5);
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test message",
            IsRead = true,
            ReadAt = originalReadAt
        };

        // Act
        notification.MarkAsRead();

        // Assert
        Assert.True(notification.IsRead);
        Assert.Equal(originalReadAt, notification.ReadAt);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsNull_ReturnsFalse()
    {
        // Arrange
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test message",
            ExpiresAt = null
        };

        // Act & Assert
        Assert.False(notification.IsExpired());
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        // Arrange
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test message",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };

        // Act & Assert
        Assert.True(notification.IsExpired());
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        // Arrange
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test message",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Act & Assert
        Assert.False(notification.IsExpired());
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var notification = new NotificationEntity
        {
            UserId = userId,
            Type = "alert",
            Title = "Test Title",
            Message = "Test Message",
            ActionUrl = "https://example.com",
            ActionText = "Click here",
            Priority = 2,
            Metadata = "{\"key\": \"value\"}"
        };

        // Assert
        Assert.Equal(userId, notification.UserId);
        Assert.Equal("alert", notification.Type);
        Assert.Equal("Test Title", notification.Title);
        Assert.Equal("Test Message", notification.Message);
        Assert.Equal("https://example.com", notification.ActionUrl);
        Assert.Equal("Click here", notification.ActionText);
        Assert.Equal(2, notification.Priority);
        Assert.Equal("{\"key\": \"value\"}", notification.Metadata);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void DefaultPriority_IsNormal()
    {
        // Arrange
        var notification = new NotificationEntity
        {
            UserId = Guid.NewGuid(),
            Type = "info",
            Title = "Test",
            Message = "Test"
        };

        // Assert - Priority 1 = Normal
        Assert.Equal(1, notification.Priority);
    }
}
