using System.Security.Claims;
using Application.UseCases.Credits.Common;
using Application.UseCases.Credits.GetCreditBalance;
using Application.UseCases.Credits.GetCreditPackages;
using Application.UseCases.Credits.GetCreditTransactions;
using Application.UseCases.Credits.GetCreditUsage;
using Application.UseCases.Credits.PurchaseCredits;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class CreditsControllerTests
{
    private readonly CreditsController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<CreditsController>> _loggerMock;

    public CreditsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<CreditsController>>();
        _controller = new CreditsController(_mediatorMock.Object, _loggerMock.Object);
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

    #endregion

    #region GetBalance Tests

    [Fact]
    public async Task GetBalance_ReturnsOk_WithCreditBalance_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedBalance = new CreditBalanceDto
        {
            Balance = 100,
            IsUnlimited = false,
            UpdatedAt = DateTime.UtcNow,
            HasSubscription = true
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetCreditBalanceQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedBalance);

        // Act
        var result = await _controller.GetBalance();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var balance = okResult.Value.Should().BeOfType<CreditBalanceDto>().Subject;
        balance.Balance.Should().Be(100);
        balance.IsUnlimited.Should().BeFalse();
        balance.HasSubscription.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalance_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetBalance();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetPackages Tests

    [Fact]
    public async Task GetPackages_ReturnsOk_WithPackageList()
    {
        // Arrange
        SetupUnauthenticatedUser(); // AllowAnonymous
        var expectedPackages = new List<CreditPackageDto>
        {
            new CreditPackageDto { Id = Guid.NewGuid(), Name = "Starter", Credits = 100, PriceInCents = 999 },
            new CreditPackageDto { Id = Guid.NewGuid(), Name = "Pro", Credits = 500, PriceInCents = 2999 }
        };

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCreditPackagesQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPackages);

        // Act
        var result = await _controller.GetPackages();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var packages = okResult.Value.Should().BeAssignableTo<List<CreditPackageDto>>().Subject;
        packages.Should().HaveCount(2);
    }

    #endregion

    #region PurchaseCredits Tests

    [Fact]
    public async Task PurchaseCredits_ReturnsOk_WithCheckoutSession_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new PurchaseCreditsRequest(
            packageId,
            "https://example.com/success",
            "https://example.com/cancel");

        var expectedResult = new PurchaseCreditsDto
        {
            SessionId = "cs_test_abc123",
            Url = "https://checkout.stripe.com/session/abc123"
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<PurchaseCreditsCommand>(cmd =>
                    cmd.UserId == userId &&
                    cmd.PackageId == packageId &&
                    cmd.SuccessUrl == request.SuccessUrl &&
                    cmd.CancelUrl == request.CancelUrl),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.PurchaseCredits(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task PurchaseCredits_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new PurchaseCreditsRequest(
            Guid.NewGuid(),
            "https://example.com/success",
            "https://example.com/cancel");

        // Act
        var result = await _controller.PurchaseCredits(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task PurchaseCredits_ReturnsNotFound_WhenPackageNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new PurchaseCreditsRequest(
            Guid.NewGuid(),
            "https://example.com/success",
            "https://example.com/cancel");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<PurchaseCreditsCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CreditPackageNotFoundException(request.PackageId));

        // Act
        var result = await _controller.PurchaseCredits(request);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().Be("Credit package not found.");
    }

    [Fact]
    public async Task PurchaseCredits_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var request = new PurchaseCreditsRequest(
            Guid.NewGuid(),
            "",  // Invalid URL
            "https://example.com/cancel");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<PurchaseCreditsCommand>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Domain.Exceptions.ValidationException("Invalid success URL"));

        // Act
        var result = await _controller.PurchaseCredits(request);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invalid success URL");
    }

    #endregion

    #region GetTransactions Tests

    [Fact]
    public async Task GetTransactions_ReturnsOk_WithTransactionList_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedTransactions = new CreditTransactionsDto
        {
            Transactions = new List<CreditTransactionDto>
            {
                new CreditTransactionDto { Id = Guid.NewGuid(), Type = "purchase", Amount = 100, BalanceAfter = 200, CreatedAt = DateTime.UtcNow },
                new CreditTransactionDto { Id = Guid.NewGuid(), Type = "consumption", Amount = -10, BalanceAfter = 190, CreatedAt = DateTime.UtcNow }
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 20,
            TotalPages = 1
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetCreditTransactionsQuery>(q =>
                    q.UserId == userId &&
                    q.Page == 1 &&
                    q.PageSize == 20),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedTransactions);

        // Act
        var result = await _controller.GetTransactions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var transactions = okResult.Value.Should().BeOfType<CreditTransactionsDto>().Subject;
        transactions.Transactions.Should().HaveCount(2);
        transactions.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetTransactions_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetTransactions();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetTransactions_SendsCorrectQueryParameters_WithFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var page = 2;
        var pageSize = 50;
        var startDate = DateTime.UtcNow.AddDays(-30);
        var endDate = DateTime.UtcNow;

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditTransactionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditTransactionsDto
            {
                Transactions = new List<CreditTransactionDto>(),
                TotalCount = 0,
                Page = page,
                PageSize = pageSize,
                TotalPages = 0
            });

        // Act
        await _controller.GetTransactions(page, pageSize, startDate, endDate);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetCreditTransactionsQuery>(q =>
                q.UserId == userId &&
                q.Page == page &&
                q.PageSize == pageSize &&
                q.StartDate == startDate &&
                q.EndDate == endDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTransactions_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCreditTransactionsQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetTransactions();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetUsage Tests

    [Fact]
    public async Task GetUsage_ReturnsOk_WithUsageSummary_WhenAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var expectedUsage = new CreditUsageDto
        {
            CurrentBalance = 150,
            ConsumedToday = 10,
            ConsumedThisWeek = 50,
            ConsumedThisMonth = 200,
            AverageDailyConsumption = 7.5,
            DaysUntilExhaustion = 20,
            Period = "month"
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetCreditUsageQuery>(q => q.UserId == userId && q.Period == "month"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUsage);

        // Act
        var result = await _controller.GetUsage();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var usage = okResult.Value.Should().BeOfType<CreditUsageDto>().Subject;
        usage.CurrentBalance.Should().Be(150);
        usage.ConsumedToday.Should().Be(10);
        usage.ConsumedThisMonth.Should().Be(200);
    }

    [Fact]
    public async Task GetUsage_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetUsage();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetUsage_SendsCorrectPeriod_WhenSpecified()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var period = "week";

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetCreditUsageQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreditUsageDto { Period = period });

        // Act
        await _controller.GetUsage(period);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetCreditUsageQuery>(q => q.UserId == userId && q.Period == period),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUsage_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<GetCreditUsageQuery>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetUsage();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    #endregion
}
