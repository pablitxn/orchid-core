using Domain.Entities;
using Domain.Enums;
using Xunit;

namespace Domain.Tests.Entities;

public class SubscriptionEntityTests
{
    [Fact]
    public void ConsumeCredits_Should_Not_Deduct_Credits_For_Unlimited_Plan()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 100,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 99.99m,
                Credits = 999999
            }
        };

        var initialCredits = subscription.Credits;
        var initialUpdatedAt = subscription.UpdatedAt;

        // Act
        subscription.ConsumeCredits(50);

        // Assert
        Assert.Equal(initialCredits, subscription.Credits); // Credits should not change
        Assert.True(subscription.UpdatedAt > initialUpdatedAt); // UpdatedAt should be updated
    }

    [Fact]
    public void ConsumeCredits_Should_Deduct_Credits_For_Regular_Plan()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 100,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Monthly100,
                Price = 29.99m,
                Credits = 100
            }
        };

        // Act
        subscription.ConsumeCredits(30);

        // Assert
        Assert.Equal(70, subscription.Credits);
    }

    [Fact]
    public void ConsumeCredits_Should_Throw_When_Insufficient_Credits_For_Regular_Plan()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 10,
            SubscriptionPlanId = Guid.NewGuid(),
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                Id = Guid.NewGuid(),
                PlanEnum = SubscriptionPlanEnum.Package10,
                Price = 9.99m,
                Credits = 10
            }
        };

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => subscription.ConsumeCredits(15));
        Assert.Contains("Insufficient credits", exception.Message);
        Assert.Contains("available 10", exception.Message);
        Assert.Contains("requested 15", exception.Message);
    }

    [Fact]
    public void ConsumeCredits_Should_Handle_Null_SubscriptionPlan()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 100,
            SubscriptionPlanId = null,
            SubscriptionPlan = null
        };

        // Act
        subscription.ConsumeCredits(30);

        // Assert
        Assert.Equal(70, subscription.Credits); // Should behave as regular plan
    }

    [Fact]
    public void HasUnlimitedCredits_Should_Return_True_For_Unlimited_Plan()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 1,
                Credits = 100
            }
        };

        // Act & Assert
        Assert.True(subscription.HasUnlimitedCredits());
    }

    [Fact]
    public void HasUnlimitedCredits_Should_Return_False_For_Regular_Plans()
    {
        // Arrange
        var plans = new[]
        {
            SubscriptionPlanEnum.Monthly100,
            SubscriptionPlanEnum.Package5,
            SubscriptionPlanEnum.Package10,
            SubscriptionPlanEnum.Package25
        };

        foreach (var plan in plans)
        {
            var subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                SubscriptionPlan = new SubscriptionPlanEntity
                {
                    PlanEnum = plan,
                    Credits = 100,
                    Price = 1
                }
            };

            // Act & Assert
            Assert.False(subscription.HasUnlimitedCredits());
        }
    }

    [Fact]
    public void HasUnlimitedCredits_Should_Return_False_When_Plan_Is_Null()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            SubscriptionPlan = null
        };

        // Act & Assert
        Assert.False(subscription.HasUnlimitedCredits());
    }

    [Fact]
    public void AddCredits_Should_Work_For_All_Plans()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 50,
            SubscriptionPlan = new SubscriptionPlanEntity
            {
                PlanEnum = SubscriptionPlanEnum.Unlimited,
                Price = 9,
                Credits = 99
            }
        };

        // Act
        subscription.AddCredits(25);

        // Assert
        Assert.Equal(75, subscription.Credits); // Even unlimited plans can add credits
    }

    [Fact]
    public void ConsumeCredits_Should_Throw_For_Invalid_Amount()
    {
        // Arrange
        var subscription = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 100
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => subscription.ConsumeCredits(0));
        Assert.Throws<ArgumentException>(() => subscription.ConsumeCredits(-5));
    }

    [Fact]
    public void AddCredits_IncreasesBalance()
    {
        // Arrange
        var sub = new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Credits = 0,
            UpdatedAt = DateTime.UtcNow.AddMinutes(-1)
        };
        var before = sub.UpdatedAt;

        // Act
        sub.AddCredits(10);

        // Assert
        Assert.Equal(10, sub.Credits);
        Assert.True(sub.UpdatedAt > before);
    }

    [Fact]
    public void AddCredits_NonPositive_Throws()
    {
        var sub = new SubscriptionEntity { Id = Guid.NewGuid(), UserId = Guid.NewGuid() };
        Assert.Throws<ArgumentException>(() => sub.AddCredits(0));
        Assert.Throws<ArgumentException>(() => sub.AddCredits(-5));
    }
}