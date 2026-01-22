using Domain.Entities;
using Domain.Enums;

namespace Domain.Tests.Entities;

public class SubscriptionPlanEntityTests
{
    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();

        var plan = new SubscriptionPlanEntity
        {
            Id = id,
            PlanEnum = SubscriptionPlanEnum.Monthly100,
            Price = 29.99m,
            Credits = 100,
            PaymentUnit = PaymentUnit.PerToken
        };

        // Assert
        Assert.Equal(id, plan.Id);
        Assert.Equal(SubscriptionPlanEnum.Monthly100, plan.PlanEnum);
        Assert.Equal(29.99m, plan.Price);
        Assert.Equal(100, plan.Credits);
        Assert.Equal(PaymentUnit.PerToken, plan.PaymentUnit);
    }

    [Fact]
    public void DefaultPaymentUnit_IsPerMessage()
    {
        // Arrange
        var plan = new SubscriptionPlanEntity
        {
            PlanEnum = SubscriptionPlanEnum.Package5,
            Price = 9.99m,
            Credits = 5
        };

        // Assert
        Assert.Equal(PaymentUnit.PerMessage, plan.PaymentUnit);
    }

    [Theory]
    [InlineData(SubscriptionPlanEnum.Monthly100, 29.99, 100)]
    [InlineData(SubscriptionPlanEnum.Package5, 4.99, 5)]
    [InlineData(SubscriptionPlanEnum.Package10, 8.99, 10)]
    [InlineData(SubscriptionPlanEnum.Package25, 19.99, 25)]
    [InlineData(SubscriptionPlanEnum.Unlimited, 99.99, 999999)]
    public void AllPlanTypes_CanBeCreated(SubscriptionPlanEnum planEnum, decimal price, int credits)
    {
        // Arrange & Act
        var plan = new SubscriptionPlanEntity
        {
            Id = Guid.NewGuid(),
            PlanEnum = planEnum,
            Price = price,
            Credits = credits
        };

        // Assert
        Assert.Equal(planEnum, plan.PlanEnum);
        Assert.Equal(price, plan.Price);
        Assert.Equal(credits, plan.Credits);
    }
}
