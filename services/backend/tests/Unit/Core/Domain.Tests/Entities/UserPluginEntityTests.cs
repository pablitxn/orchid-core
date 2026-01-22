using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserPluginEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var purchasedAt = DateTime.UtcNow;
        var expiresAt = DateTime.UtcNow.AddMonths(1);

        var userPlugin = new UserPluginEntity
        {
            Id = id,
            UserId = userId,
            PluginId = pluginId,
            PurchasedAt = purchasedAt,
            ExpiresAt = expiresAt,
            IsActive = true
        };

        // Assert
        Assert.Equal(id, userPlugin.Id);
        Assert.Equal(userId, userPlugin.UserId);
        Assert.Equal(pluginId, userPlugin.PluginId);
        Assert.Equal(purchasedAt, userPlugin.PurchasedAt);
        Assert.Equal(expiresAt, userPlugin.ExpiresAt);
        Assert.True(userPlugin.IsActive);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var userPlugin = new UserPluginEntity();

        // Assert
        Assert.True(userPlugin.IsActive);
        Assert.Null(userPlugin.ExpiresAt);
    }

    [Fact]
    public void PermanentPurchase_HasNoExpiration()
    {
        // Arrange
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PluginId = Guid.NewGuid(),
            ExpiresAt = null,
            IsActive = true
        };

        // Assert
        Assert.Null(userPlugin.ExpiresAt);
    }

    [Fact]
    public void SubscriptionPurchase_HasExpiration()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddMonths(1);
        var userPlugin = new UserPluginEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            PluginId = Guid.NewGuid(),
            ExpiresAt = expiresAt,
            IsActive = true
        };

        // Assert
        Assert.NotNull(userPlugin.ExpiresAt);
        Assert.Equal(expiresAt, userPlugin.ExpiresAt);
    }
}
