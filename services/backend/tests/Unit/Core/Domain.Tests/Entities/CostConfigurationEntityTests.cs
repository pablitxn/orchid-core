using Domain.Entities;

namespace Domain.Tests.Entities;

public class CostConfigurationEntityTests
{
    [Fact]
    public void IsEffective_WhenNotActive_ReturnsFalse()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = false
        };

        // Act & Assert
        Assert.False(config.IsEffective());
    }

    [Fact]
    public void IsEffective_WhenActiveAndNoDateRange_ReturnsTrue()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = true,
            EffectiveFrom = null,
            EffectiveTo = null
        };

        // Act & Assert
        Assert.True(config.IsEffective());
    }

    [Fact]
    public void IsEffective_WhenBeforeEffectiveFrom_ReturnsFalse()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(1),
            EffectiveTo = null
        };

        // Act & Assert
        Assert.False(config.IsEffective());
    }

    [Fact]
    public void IsEffective_WhenAfterEffectiveTo_ReturnsFalse()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = true,
            EffectiveFrom = null,
            EffectiveTo = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        Assert.False(config.IsEffective());
    }

    [Fact]
    public void IsEffective_WhenWithinDateRange_ReturnsTrue()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            EffectiveTo = DateTime.UtcNow.AddDays(1)
        };

        // Act & Assert
        Assert.True(config.IsEffective());
    }

    [Fact]
    public void IsEffective_WithSpecificDate_ChecksAgainstThatDate()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "message_fixed",
            CreditCost = 5,
            IsActive = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-10),
            EffectiveTo = DateTime.UtcNow.AddDays(-5)
        };

        var dateInRange = DateTime.UtcNow.AddDays(-7);
        var dateOutOfRange = DateTime.UtcNow;

        // Act & Assert
        Assert.True(config.IsEffective(dateInRange));
        Assert.False(config.IsEffective(dateOutOfRange));
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var resourceId = Guid.NewGuid();

        var config = new CostConfigurationEntity
        {
            CostType = "plugin_usage",
            ResourceId = resourceId,
            ResourceName = "Test Plugin",
            CreditCost = 10,
            CostPer1kTokens = 0.5m,
            IsActive = true,
            Priority = 5,
            CreatedBy = "admin",
            UpdatedBy = "admin",
            Metadata = "{\"config\": true}"
        };

        // Assert
        Assert.Equal("plugin_usage", config.CostType);
        Assert.Equal(resourceId, config.ResourceId);
        Assert.Equal("Test Plugin", config.ResourceName);
        Assert.Equal(10, config.CreditCost);
        Assert.Equal(0.5m, config.CostPer1kTokens);
        Assert.True(config.IsActive);
        Assert.Equal(5, config.Priority);
        Assert.Equal("admin", config.CreatedBy);
        Assert.Equal("admin", config.UpdatedBy);
        Assert.Equal("{\"config\": true}", config.Metadata);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var config = new CostConfigurationEntity
        {
            CostType = "test"
        };

        // Assert
        Assert.True(config.IsActive);
        Assert.Equal(0, config.Priority);
        Assert.Null(config.EffectiveFrom);
        Assert.Null(config.EffectiveTo);
    }
}
