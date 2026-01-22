using System.Security.Claims;
using Application.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class CreditHistoryControllerTests
{
    private readonly CreditHistoryController _controller;
    private readonly Mock<ICreditConsumptionRepository> _creditConsumptionRepositoryMock;
    private readonly Mock<IMessageCostRepository> _messageCostRepositoryMock;
    private readonly Mock<ISubscriptionRepository> _subscriptionRepositoryMock;
    private readonly Mock<ILogger<CreditHistoryController>> _loggerMock;

    public CreditHistoryControllerTests()
    {
        _creditConsumptionRepositoryMock = new Mock<ICreditConsumptionRepository>();
        _messageCostRepositoryMock = new Mock<IMessageCostRepository>();
        _subscriptionRepositoryMock = new Mock<ISubscriptionRepository>();
        _loggerMock = new Mock<ILogger<CreditHistoryController>>();
        _controller = new CreditHistoryController(
            _creditConsumptionRepositoryMock.Object,
            _messageCostRepositoryMock.Object,
            _subscriptionRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    private void SetupUserWithInvalidGuid()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "not-a-valid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private CreditConsumptionEntity CreateMockConsumption(Guid userId, string type = "message", int credits = 10)
    {
        return new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConsumptionType = type,
            ResourceId = Guid.NewGuid(),
            ResourceName = "Test Resource",
            CreditsConsumed = credits,
            ConsumedAt = DateTime.UtcNow,
            BalanceAfter = 90
        };
    }

    private MessageCostEntity CreateMockMessageCost(Guid userId, int totalCredits = 5)
    {
        return new MessageCostEntity
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            UserId = userId,
            BillingMethod = "tokens",
            TokensConsumed = 100,
            CostPerToken = 0.05m,
            TotalCredits = totalCredits,
            HasPluginUsage = false,
            HasWorkflowUsage = false,
            AdditionalCredits = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    private SubscriptionEntity CreateMockSubscription(Guid userId, int credits = 100)
    {
        return new SubscriptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = credits,
            AutoRenew = true,
            ExpiresAt = DateTime.UtcNow.AddMonths(1)
        };
    }

    #endregion

    #region GetConsumptionHistory Tests

    [Fact]
    public async Task GetConsumptionHistory_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var consumptions = new List<CreditConsumptionEntity>
        {
            CreateMockConsumption(userId),
            CreateMockConsumption(userId, "plugin", 20)
        };

        _creditConsumptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumptions);

        // Act
        var result = await _controller.GetConsumptionHistory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConsumptionHistory_ReturnsOk_WithDateFilter()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var consumptions = new List<CreditConsumptionEntity>
        {
            CreateMockConsumption(userId)
        };

        _creditConsumptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(consumptions);

        // Act
        var result = await _controller.GetConsumptionHistory(50, startDate, endDate);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConsumptionHistory_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetConsumptionHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConsumptionHistory_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetConsumptionHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConsumptionHistory_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _creditConsumptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetConsumptionHistory();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetMessageCostHistory Tests

    [Fact]
    public async Task GetMessageCostHistory_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var messageCosts = new List<MessageCostEntity>
        {
            CreateMockMessageCost(userId),
            CreateMockMessageCost(userId, 10)
        };

        _messageCostRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageCosts);

        // Act
        var result = await _controller.GetMessageCostHistory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMessageCostHistory_ReturnsOk_WithCustomLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var messageCosts = new List<MessageCostEntity>
        {
            CreateMockMessageCost(userId)
        };

        _messageCostRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(messageCosts);

        // Act
        var result = await _controller.GetMessageCostHistory(10);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetMessageCostHistory_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetMessageCostHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMessageCostHistory_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetMessageCostHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetMessageCostHistory_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _messageCostRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, 50, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetMessageCostHistory();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetConsumptionSummary Tests

    [Fact]
    public async Task GetConsumptionSummary_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var subscription = CreateMockSubscription(userId);

        _messageCostRepositoryMock.Setup(r => r.GetTotalCostByUserIdAsync(userId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.GetConsumptionSummary();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Theory]
    [InlineData("day")]
    [InlineData("week")]
    [InlineData("month")]
    [InlineData("year")]
    public async Task GetConsumptionSummary_ReturnsOk_ForDifferentPeriods(string period)
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var subscription = CreateMockSubscription(userId);

        _messageCostRepositoryMock.Setup(r => r.GetTotalCostByUserIdAsync(userId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50);
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.GetConsumptionSummary(period);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConsumptionSummary_ReturnsOk_WhenNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _messageCostRepositoryMock.Setup(r => r.GetTotalCostByUserIdAsync(userId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var result = await _controller.GetConsumptionSummary();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetConsumptionSummary_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetConsumptionSummary();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConsumptionSummary_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetConsumptionSummary();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetConsumptionSummary_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _messageCostRepositoryMock.Setup(r => r.GetTotalCostByUserIdAsync(userId, It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetConsumptionSummary();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetCreditBalance Tests

    [Fact]
    public async Task GetCreditBalance_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var subscription = CreateMockSubscription(userId, 500);

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.GetCreditBalance();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCreditBalance_ReturnsOk_WhenNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionEntity?)null);

        // Act
        var result = await _controller.GetCreditBalance();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetCreditBalance_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetCreditBalance();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCreditBalance_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetCreditBalance();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetCreditBalance_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _subscriptionRepositoryMock.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetCreditBalance();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion
}
