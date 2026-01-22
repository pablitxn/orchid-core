using Domain.Entities;

namespace Domain.Tests.Entities;

public class MessageCostEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var messageCost = new MessageCostEntity
        {
            Id = id,
            MessageId = messageId,
            UserId = userId,
            BillingMethod = "tokens",
            TokensConsumed = 500,
            CostPerToken = 0.001m,
            FixedRate = null,
            TotalCredits = 5,
            HasPluginUsage = true,
            HasWorkflowUsage = false,
            AdditionalCredits = 10
        };

        // Assert
        Assert.Equal(id, messageCost.Id);
        Assert.Equal(messageId, messageCost.MessageId);
        Assert.Equal(userId, messageCost.UserId);
        Assert.Equal("tokens", messageCost.BillingMethod);
        Assert.Equal(500, messageCost.TokensConsumed);
        Assert.Equal(0.001m, messageCost.CostPerToken);
        Assert.Null(messageCost.FixedRate);
        Assert.Equal(5, messageCost.TotalCredits);
        Assert.True(messageCost.HasPluginUsage);
        Assert.False(messageCost.HasWorkflowUsage);
        Assert.Equal(10, messageCost.AdditionalCredits);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var messageCost = new MessageCostEntity();

        // Assert
        Assert.Equal("tokens", messageCost.BillingMethod);
        Assert.False(messageCost.HasPluginUsage);
        Assert.False(messageCost.HasWorkflowUsage);
        Assert.Equal(0, messageCost.AdditionalCredits);
    }

    [Fact]
    public void FixedRateBilling_CanBeConfigured()
    {
        // Arrange
        var messageCost = new MessageCostEntity
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            BillingMethod = "fixed",
            FixedRate = 5.0m,
            TokensConsumed = null,
            CostPerToken = null,
            TotalCredits = 5
        };

        // Assert
        Assert.Equal("fixed", messageCost.BillingMethod);
        Assert.Equal(5.0m, messageCost.FixedRate);
        Assert.Null(messageCost.TokensConsumed);
        Assert.Null(messageCost.CostPerToken);
    }
}
