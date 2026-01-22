using Domain.Entities;

namespace Domain.Tests.Entities;

public class CreditConsumptionEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();

        var consumption = new CreditConsumptionEntity
        {
            Id = id,
            UserId = userId,
            ConsumptionType = "plugin",
            ResourceId = resourceId,
            ResourceName = "Web Search Plugin",
            CreditsConsumed = 5,
            Metadata = "{\"query\": \"test search\"}",
            BalanceAfter = 95
        };

        // Assert
        Assert.Equal(id, consumption.Id);
        Assert.Equal(userId, consumption.UserId);
        Assert.Equal("plugin", consumption.ConsumptionType);
        Assert.Equal(resourceId, consumption.ResourceId);
        Assert.Equal("Web Search Plugin", consumption.ResourceName);
        Assert.Equal(5, consumption.CreditsConsumed);
        Assert.Equal("{\"query\": \"test search\"}", consumption.Metadata);
        Assert.Equal(95, consumption.BalanceAfter);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var consumption = new CreditConsumptionEntity();

        // Assert
        Assert.Equal(string.Empty, consumption.ConsumptionType);
        Assert.Equal(string.Empty, consumption.ResourceName);
        Assert.Null(consumption.ResourceId);
        Assert.Null(consumption.Metadata);
    }

    [Theory]
    [InlineData("plugin")]
    [InlineData("workflow")]
    [InlineData("message")]
    public void AllConsumptionTypes_CanBeCreated(string consumptionType)
    {
        // Arrange & Act
        var consumption = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ConsumptionType = consumptionType,
            ResourceName = "Test Resource",
            CreditsConsumed = 10,
            BalanceAfter = 90
        };

        // Assert
        Assert.Equal(consumptionType, consumption.ConsumptionType);
    }
}
