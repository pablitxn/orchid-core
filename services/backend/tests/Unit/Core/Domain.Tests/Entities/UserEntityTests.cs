using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;

        var user = new UserEntity
        {
            Id = id,
            Email = "test@example.com",
            Name = "Test User",
            AvatarUrl = "https://example.com/avatar.png",
            CreatedAt = createdAt,
            PasswordHash = "hashed-password",
            PasswordResetToken = "reset-token",
            PasswordResetExpiry = DateTime.UtcNow.AddHours(1),
            PasswordChangedAt = DateTime.UtcNow
        };

        // Assert
        Assert.Equal(id, user.Id);
        Assert.Equal("test@example.com", user.Email);
        Assert.Equal("Test User", user.Name);
        Assert.Equal("https://example.com/avatar.png", user.AvatarUrl);
        Assert.Equal(createdAt, user.CreatedAt);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal("reset-token", user.PasswordResetToken);
        Assert.NotNull(user.PasswordResetExpiry);
        Assert.NotNull(user.PasswordChangedAt);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var user = new UserEntity
        {
            Id = Guid.NewGuid()
        };

        // Assert
        Assert.False(user.SubscriptionActive);
        Assert.Empty(user.UserRoles);
        Assert.Equal(string.Empty, user.PasswordHash);
        Assert.Null(user.DeletedAt);
        Assert.Null(user.PasswordResetToken);
        Assert.Null(user.PasswordResetExpiry);
    }

    [Fact]
    public void UserRoles_CanBeModified()
    {
        // Arrange
        var user = new UserEntity { Id = Guid.NewGuid() };
        var role = new UserRoleEntity
        {
            UserId = user.Id,
            RoleId = Guid.NewGuid()
        };

        // Act
        user.UserRoles.Add(role);

        // Assert
        Assert.Single(user.UserRoles);
        Assert.Equal(role, user.UserRoles[0]);
    }
}
