using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserBillingPreferenceEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var preference = new UserBillingPreferenceEntity
        {
            Id = id,
            UserId = userId,
            MessageBillingMethod = "fixed",
            TokenRate = 0.002m,
            FixedMessageRate = 10,
            LowCreditThreshold = 50,
            EnableLowCreditAlerts = false
        };

        // Assert
        Assert.Equal(id, preference.Id);
        Assert.Equal(userId, preference.UserId);
        Assert.Equal("fixed", preference.MessageBillingMethod);
        Assert.Equal(0.002m, preference.TokenRate);
        Assert.Equal(10, preference.FixedMessageRate);
        Assert.Equal(50, preference.LowCreditThreshold);
        Assert.False(preference.EnableLowCreditAlerts);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange
        var preference = new UserBillingPreferenceEntity();

        // Assert
        Assert.Equal("tokens", preference.MessageBillingMethod);
        Assert.Equal(0.001m, preference.TokenRate);
        Assert.Equal(5, preference.FixedMessageRate);
        Assert.Equal(100, preference.LowCreditThreshold);
        Assert.True(preference.EnableLowCreditAlerts);
    }

    [Theory]
    [InlineData("tokens")]
    [InlineData("fixed")]
    public void BillingMethod_CanBeSetToValidValues(string billingMethod)
    {
        // Arrange & Act
        var preference = new UserBillingPreferenceEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            MessageBillingMethod = billingMethod
        };

        // Assert
        Assert.Equal(billingMethod, preference.MessageBillingMethod);
    }
}
