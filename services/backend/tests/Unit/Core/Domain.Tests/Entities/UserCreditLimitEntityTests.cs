using Domain.Entities;

namespace Domain.Tests.Entities;

public class UserCreditLimitEntityTests
{
    [Fact]
    public void IsWithinLimit_WhenNotActive_ReturnsTrue()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 100,
            IsActive = false,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act & Assert
        Assert.True(limit.IsWithinLimit(50));
    }

    [Fact]
    public void IsWithinLimit_WhenWithinLimit_ReturnsTrue()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 50,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act & Assert
        Assert.True(limit.IsWithinLimit(50));
    }

    [Fact]
    public void IsWithinLimit_WhenExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 60,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act & Assert
        Assert.False(limit.IsWithinLimit(50));
    }

    [Fact]
    public void IsWithinLimit_WhenPeriodExpired_ResetsAndReturnsTrue()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 100,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddDays(-2),
            PeriodEndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        Assert.True(limit.IsWithinLimit(50));
    }

    [Fact]
    public void ConsumeCredits_IncreasesConsumedCredits()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 0,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act
        limit.ConsumeCredits(30);

        // Assert
        Assert.Equal(30, limit.ConsumedCredits);
    }

    [Fact]
    public void ConsumeCredits_WhenPeriodExpired_ResetsThenConsumes()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 100,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddDays(-2),
            PeriodEndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act
        limit.ConsumeCredits(30);

        // Assert
        Assert.Equal(30, limit.ConsumedCredits);
    }

    [Fact]
    public void GetRemainingCredits_ReturnsCorrectValue()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 30,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act & Assert
        Assert.Equal(70, limit.GetRemainingCredits());
    }

    [Fact]
    public void GetRemainingCredits_WhenPeriodExpired_ReturnsMaxCredits()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 80,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddDays(-2),
            PeriodEndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act & Assert
        Assert.Equal(100, limit.GetRemainingCredits());
    }

    [Fact]
    public void GetRemainingCredits_WhenOverConsumed_ReturnsZero()
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = "daily",
            MaxCredits = 100,
            ConsumedCredits = 150,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddHours(-1),
            PeriodEndDate = DateTime.UtcNow.AddHours(23)
        };

        // Act & Assert
        Assert.Equal(0, limit.GetRemainingCredits());
    }

    [Theory]
    [InlineData("daily", 1)]
    [InlineData("weekly", 7)]
    [InlineData("monthly", 30)] // Approximately, depends on month
    public void ResetPeriod_SetsCorrectEndDate(string limitType, int expectedDays)
    {
        // Arrange
        var limit = new UserCreditLimitEntity
        {
            UserId = Guid.NewGuid(),
            LimitType = limitType,
            MaxCredits = 100,
            ConsumedCredits = 50,
            IsActive = true,
            PeriodStartDate = DateTime.UtcNow.AddDays(-5),
            PeriodEndDate = DateTime.UtcNow.AddDays(-1)
        };

        // Act - trigger reset by calling IsWithinLimit
        limit.IsWithinLimit(10);

        // Assert
        Assert.Equal(0, limit.ConsumedCredits);

        // For monthly, we need to be more flexible with the assertion
        if (limitType == "monthly")
        {
            Assert.True(limit.PeriodEndDate > DateTime.UtcNow.AddDays(27));
        }
        else
        {
            var expectedEnd = limit.PeriodStartDate.AddDays(expectedDays);
            Assert.Equal(expectedEnd.Date, limit.PeriodEndDate.Date);
        }
    }

    [Fact]
    public void Properties_AreSetCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var periodStart = DateTime.UtcNow;
        var periodEnd = DateTime.UtcNow.AddDays(7);

        var limit = new UserCreditLimitEntity
        {
            UserId = userId,
            LimitType = "weekly",
            MaxCredits = 500,
            ConsumedCredits = 100,
            PeriodStartDate = periodStart,
            PeriodEndDate = periodEnd,
            IsActive = true,
            ResourceType = "plugin",
            Version = 3
        };

        // Assert
        Assert.Equal(userId, limit.UserId);
        Assert.Equal("weekly", limit.LimitType);
        Assert.Equal(500, limit.MaxCredits);
        Assert.Equal(100, limit.ConsumedCredits);
        Assert.Equal(periodStart, limit.PeriodStartDate);
        Assert.Equal(periodEnd, limit.PeriodEndDate);
        Assert.True(limit.IsActive);
        Assert.Equal("plugin", limit.ResourceType);
        Assert.Equal(3, limit.Version);
    }
}
