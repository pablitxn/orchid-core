using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Infrastructure.Tests.Services;

public class CreditLimitServiceTests
{
    private readonly Mock<IUserCreditLimitRepository> _limitRepositoryMock;
    private readonly Mock<ILogger<CreditLimitService>> _loggerMock;
    private readonly CreditLimitService _service;

    public CreditLimitServiceTests()
    {
        _limitRepositoryMock = new Mock<IUserCreditLimitRepository>();
        _loggerMock = new Mock<ILogger<CreditLimitService>>();
        _service = new CreditLimitService(_limitRepositoryMock.Object, _loggerMock.Object);
    }

    #region CheckLimitsAsync Tests

    [Fact]
    public async Task CheckLimitsAsync_Should_ReturnWithinLimits_When_NoLimitsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<UserCreditLimitEntity>());

        // Act
        var result = await _service.CheckLimitsAsync(userId, 100);

        // Assert
        result.IsWithinLimits.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_ReturnWithinLimits_When_UnderLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var limit = CreateLimit(userId, "daily", 1000, 500);

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { limit });

        // Act
        var result = await _service.CheckLimitsAsync(userId, 400);

        // Assert
        result.IsWithinLimits.Should().BeTrue();
        result.Violations.Should().BeEmpty();
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_ReturnViolation_When_OverLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var limit = CreateLimit(userId, "daily", 1000, 800);

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { limit });

        // Act
        var result = await _service.CheckLimitsAsync(userId, 300);

        // Assert
        result.IsWithinLimits.Should().BeFalse();
        result.Violations.Should().HaveCount(1);
        result.Violations![0].LimitType.Should().Be("daily");
        result.Violations[0].MaxCredits.Should().Be(1000);
        result.Violations[0].ConsumedCredits.Should().Be(800);
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_ReturnMultipleViolations_When_MultipleLimitsExceeded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dailyLimit = CreateLimit(userId, "daily", 100, 90);
        var monthlyLimit = CreateLimit(userId, "monthly", 1000, 990);

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dailyLimit, monthlyLimit });

        // Act
        var result = await _service.CheckLimitsAsync(userId, 50);

        // Assert
        result.IsWithinLimits.Should().BeFalse();
        result.Violations.Should().HaveCount(2);
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_SkipInactiveLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var inactiveLimit = CreateLimit(userId, "daily", 100, 200);
        inactiveLimit.IsActive = false;

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { inactiveLimit });

        // Act
        var result = await _service.CheckLimitsAsync(userId, 50);

        // Assert
        result.IsWithinLimits.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_FilterByResourceType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatLimit = CreateLimit(userId, "daily", 100, 90, "chat");
        var pluginLimit = CreateLimit(userId, "daily", 500, 100, "plugin");

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { chatLimit, pluginLimit });

        // Act - Check plugin resource type
        var result = await _service.CheckLimitsAsync(userId, 50, "plugin");

        // Assert
        result.IsWithinLimits.Should().BeTrue();
    }

    [Fact]
    public async Task CheckLimitsAsync_Should_ReturnTrue_OnException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.CheckLimitsAsync(userId, 100);

        // Assert - Fail open behavior
        result.IsWithinLimits.Should().BeTrue();
    }

    #endregion

    #region ConsumeLimitsAsync Tests

    [Fact]
    public async Task ConsumeLimitsAsync_Should_ReturnTrue_OnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.ConsumeLimitsAsync(userId, 100, null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ConsumeLimitsAsync(userId, 100);

        // Assert
        result.Should().BeTrue();
        _limitRepositoryMock.Verify(x => x.ConsumeLimitsAsync(userId, 100, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeLimitsAsync_Should_PassResourceType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var resourceType = "chat";
        _limitRepositoryMock
            .Setup(x => x.ConsumeLimitsAsync(userId, 50, resourceType, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.ConsumeLimitsAsync(userId, 50, resourceType);

        // Assert
        result.Should().BeTrue();
        _limitRepositoryMock.Verify(x => x.ConsumeLimitsAsync(userId, 50, resourceType, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumeLimitsAsync_Should_ReturnFalse_OnException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.ConsumeLimitsAsync(userId, 100, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _service.ConsumeLimitsAsync(userId, 100);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetUserLimitsAsync Tests

    [Fact]
    public async Task GetUserLimitsAsync_Should_ReturnAllLimits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var dailyLimit = CreateLimit(userId, "daily", 100, 50);
        var monthlyLimit = CreateLimit(userId, "monthly", 1000, 200);

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { dailyLimit, monthlyLimit });

        // Act
        var result = await _service.GetUserLimitsAsync(userId);

        // Assert
        result.UserId.Should().Be(userId);
        result.Limits.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUserLimitsAsync_Should_MapLimitPropertiesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var limitId = Guid.NewGuid();
        var limit = new UserCreditLimitEntity
        {
            Id = limitId,
            UserId = userId,
            LimitType = "daily",
            MaxCredits = 1000,
            ConsumedCredits = 300,
            PeriodStartDate = DateTime.UtcNow.AddHours(-12),
            PeriodEndDate = DateTime.UtcNow.AddHours(12),
            IsActive = true,
            ResourceType = "chat"
        };

        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { limit });

        // Act
        var result = await _service.GetUserLimitsAsync(userId);

        // Assert
        var dto = result.Limits.Single();
        dto.Id.Should().Be(limitId);
        dto.LimitType.Should().Be("daily");
        dto.MaxCredits.Should().Be(1000);
        dto.ConsumedCredits.Should().Be(300);
        dto.RemainingCredits.Should().Be(700);
        dto.IsActive.Should().BeTrue();
        dto.ResourceType.Should().Be("chat");
    }

    [Fact]
    public async Task GetUserLimitsAsync_Should_ReturnEmptyList_When_NoLimitsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserIdAsync(userId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<UserCreditLimitEntity>());

        // Act
        var result = await _service.GetUserLimitsAsync(userId);

        // Assert
        result.UserId.Should().Be(userId);
        result.Limits.Should().BeEmpty();
    }

    #endregion

    #region SetUserLimitAsync Tests

    [Fact]
    public async Task SetUserLimitAsync_Should_CreateNewLimit_When_NotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity?)null);

        _limitRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserCreditLimitEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity l, CancellationToken _) => l);

        // Act
        var result = await _service.SetUserLimitAsync(userId, "daily", 1000);

        // Assert
        result.LimitType.Should().Be("daily");
        result.MaxCredits.Should().Be(1000);
        _limitRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<UserCreditLimitEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetUserLimitAsync_Should_UpdateExistingLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingLimit = CreateLimit(userId, "daily", 500, 100);

        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLimit);

        // Act
        var result = await _service.SetUserLimitAsync(userId, "daily", 1000);

        // Assert
        result.MaxCredits.Should().Be(1000);
        result.IsActive.Should().BeTrue();
        _limitRepositoryMock.Verify(x => x.UpdateAsync(It.Is<UserCreditLimitEntity>(l => l.MaxCredits == 1000), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("daily", 1)]
    [InlineData("weekly", 7)]
    [InlineData("monthly", 30)] // Approximate
    public async Task SetUserLimitAsync_Should_SetCorrectPeriodEnd(string limitType, int expectedDays)
    {
        // Arrange
        var userId = Guid.NewGuid();
        UserCreditLimitEntity? capturedEntity = null;

        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, limitType, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity?)null);

        _limitRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserCreditLimitEntity>(), It.IsAny<CancellationToken>()))
            .Callback<UserCreditLimitEntity, CancellationToken>((e, _) => capturedEntity = e)
            .ReturnsAsync((UserCreditLimitEntity l, CancellationToken _) => l);

        // Act
        await _service.SetUserLimitAsync(userId, limitType, 1000);

        // Assert
        capturedEntity.Should().NotBeNull();
        var expectedEnd = limitType == "monthly"
            ? capturedEntity!.PeriodStartDate.AddMonths(1)
            : capturedEntity!.PeriodStartDate.AddDays(expectedDays);

        capturedEntity.PeriodEndDate.Should().BeCloseTo(expectedEnd, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task SetUserLimitAsync_Should_HandleResourceType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", "chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity?)null);

        _limitRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<UserCreditLimitEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity l, CancellationToken _) => l);

        // Act
        var result = await _service.SetUserLimitAsync(userId, "daily", 1000, "chat");

        // Assert
        result.ResourceType.Should().Be("chat");
    }

    #endregion

    #region RemoveUserLimitAsync Tests

    [Fact]
    public async Task RemoveUserLimitAsync_Should_DeactivateExistingLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingLimit = CreateLimit(userId, "daily", 1000, 100);

        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLimit);

        // Act
        await _service.RemoveUserLimitAsync(userId, "daily");

        // Assert
        _limitRepositoryMock.Verify(x => x.UpdateAsync(
            It.Is<UserCreditLimitEntity>(l => l.IsActive == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveUserLimitAsync_Should_NotThrow_When_LimitNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserCreditLimitEntity?)null);

        // Act
        var act = () => _service.RemoveUserLimitAsync(userId, "daily");

        // Assert
        await act.Should().NotThrowAsync();
        _limitRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<UserCreditLimitEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveUserLimitAsync_Should_HandleResourceType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingLimit = CreateLimit(userId, "daily", 1000, 100, "chat");

        _limitRepositoryMock
            .Setup(x => x.GetByUserAndTypeAsync(userId, "daily", "chat", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingLimit);

        // Act
        await _service.RemoveUserLimitAsync(userId, "daily", "chat");

        // Assert
        _limitRepositoryMock.Verify(x => x.GetByUserAndTypeAsync(userId, "daily", "chat", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    private static UserCreditLimitEntity CreateLimit(
        Guid userId,
        string limitType,
        int maxCredits,
        int consumedCredits,
        string? resourceType = null)
    {
        return new UserCreditLimitEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LimitType = limitType,
            MaxCredits = maxCredits,
            ConsumedCredits = consumedCredits,
            PeriodStartDate = DateTime.UtcNow.AddHours(-12),
            PeriodEndDate = DateTime.UtcNow.AddHours(12),
            IsActive = true,
            ResourceType = resourceType
        };
    }
}
