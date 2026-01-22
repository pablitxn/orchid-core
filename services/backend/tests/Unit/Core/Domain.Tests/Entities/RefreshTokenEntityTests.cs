using Domain.Entities;

namespace Domain.Tests.Entities;

public class RefreshTokenEntityTests
{
    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act & Assert
        Assert.True(token.IsExpired);
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.False(token.IsExpired);
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtIsSet_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.True(token.IsRevoked);
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtIsNull_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        // Act & Assert
        Assert.False(token.IsRevoked);
    }

    [Fact]
    public void IsActive_WhenNotRevokedAndNotExpired_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        // Act & Assert
        Assert.True(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.False(token.IsActive);
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshTokenEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "test-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = null
        };

        // Act & Assert
        Assert.False(token.IsActive);
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddHours(1);

        var token = new RefreshTokenEntity
        {
            Id = id,
            UserId = userId,
            Token = "test-token-value",
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            CreatedByIp = "127.0.0.1",
            RevokedByIp = "192.168.1.1",
            ReplacedByToken = "new-token",
            RevokeReason = "Manual revocation"
        };

        // Assert
        Assert.Equal(id, token.Id);
        Assert.Equal(userId, token.UserId);
        Assert.Equal("test-token-value", token.Token);
        Assert.Equal(expiresAt, token.ExpiresAt);
        Assert.Equal(createdAt, token.CreatedAt);
        Assert.Equal("127.0.0.1", token.CreatedByIp);
        Assert.Equal("192.168.1.1", token.RevokedByIp);
        Assert.Equal("new-token", token.ReplacedByToken);
        Assert.Equal("Manual revocation", token.RevokeReason);
    }
}
