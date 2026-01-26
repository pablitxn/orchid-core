using System.Security.Claims;
using Application.UseCases.Billing.Common;
using Application.UseCases.Billing.GetBillingInvoices;
using Application.UseCases.Billing.GetBillingPaymentMethods;
using Application.UseCases.Billing.GetBillingSubscription;
using Application.UseCases.Billing.GetBillingSummary;
using Application.UseCases.Subscription.Common;
using Domain.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class BillingControllerTests
{
    private readonly BillingController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<BillingController>> _loggerMock;

    public BillingControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<BillingController>>();
        _controller = new BillingController(_mediatorMock.Object, _loggerMock.Object);
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

    private SubscriptionDto CreateMockSubscription(Guid userId, int credits = 100)
    {
        return new SubscriptionDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = credits,
            AutoRenew = true,
            Status = SubscriptionStatus.Active,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddMonths(1)
        };
    }

    private List<InvoiceDto> CreateMockInvoices()
    {
        return new List<InvoiceDto>
        {
            new()
            {
                Id = "inv_123",
                Number = "INV-001",
                Status = "paid",
                AmountDue = 1000,
                AmountPaid = 1000,
                Currency = "usd",
                CreatedAt = DateTime.UtcNow.AddDays(-30),
                PaidAt = DateTime.UtcNow.AddDays(-30)
            },
            new()
            {
                Id = "inv_124",
                Number = "INV-002",
                Status = "paid",
                AmountDue = 1000,
                AmountPaid = 1000,
                Currency = "usd",
                CreatedAt = DateTime.UtcNow,
                PaidAt = DateTime.UtcNow
            }
        };
    }

    private List<PaymentMethodDto> CreateMockPaymentMethods()
    {
        return new List<PaymentMethodDto>
        {
            new()
            {
                Id = "pm_123",
                Type = "card",
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-1),
                Card = new CardDetailsDto
                {
                    Brand = "visa",
                    Last4 = "4242",
                    ExpMonth = 12,
                    ExpYear = 2027
                }
            }
        };
    }

    private BillingSummaryDto CreateMockBillingSummary(Guid userId)
    {
        return new BillingSummaryDto
        {
            Subscription = CreateMockSubscription(userId),
            CreditBalance = 100,
            IsUnlimited = false,
            PaymentMethodCount = 1,
            CreditsConsumedThisMonth = 50,
            CurrentPeriodSpend = 1000
        };
    }

    #endregion

    #region GetSubscription Tests

    [Fact]
    public async Task GetSubscription_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var subscription = CreateMockSubscription(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.GetSubscription();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(subscription);
    }

    [Fact]
    public async Task GetSubscription_ReturnsNotFound_WhenNoSubscription()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingSubscriptionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionDto?)null);

        // Act
        var result = await _controller.GetSubscription();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetSubscription_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetSubscription();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSubscription_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetSubscription();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region GetInvoices Tests

    [Fact]
    public async Task GetInvoices_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var invoices = CreateMockInvoices();

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingInvoicesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        // Act
        var result = await _controller.GetInvoices();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(invoices);
    }

    [Fact]
    public async Task GetInvoices_ReturnsOk_WithCustomLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var invoices = CreateMockInvoices().Take(1).ToList();

        _mediatorMock.Setup(m => m.Send(It.Is<GetBillingInvoicesQuery>(q => q.Limit == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(invoices);

        // Act
        var result = await _controller.GetInvoices(limit: 1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetInvoices_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetInvoices();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetInvoices_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingInvoicesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe API error"));

        // Act
        var result = await _controller.GetInvoices();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetPaymentMethods Tests

    [Fact]
    public async Task GetPaymentMethods_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var paymentMethods = CreateMockPaymentMethods();

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingPaymentMethodsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(paymentMethods);

        // Act
        var result = await _controller.GetPaymentMethods();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(paymentMethods);
    }

    [Fact]
    public async Task GetPaymentMethods_ReturnsEmptyList_WhenNoPaymentMethods()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingPaymentMethodsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PaymentMethodDto>());

        // Act
        var result = await _controller.GetPaymentMethods();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        var list = okResult.Value as List<PaymentMethodDto>;
        list.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPaymentMethods_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetPaymentMethods();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetPaymentMethods_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingPaymentMethodsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Stripe API error"));

        // Act
        var result = await _controller.GetPaymentMethods();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetSummary Tests

    [Fact]
    public async Task GetSummary_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var summary = CreateMockBillingSummary(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.GetSummary();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = (OkObjectResult)result;
        okResult.Value.Should().BeEquivalentTo(summary);
    }

    [Fact]
    public async Task GetSummary_PassesReturnUrl_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var returnUrl = "https://example.com/billing";
        var summary = CreateMockBillingSummary(userId);

        _mediatorMock.Setup(m => m.Send(
            It.Is<GetBillingSummaryQuery>(q => q.ReturnUrl == returnUrl),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary);

        // Act
        var result = await _controller.GetSummary(returnUrl);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetBillingSummaryQuery>(q => q.ReturnUrl == returnUrl),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSummary_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetSummary();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetSummary_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetBillingSummaryQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetSummary();

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion
}
