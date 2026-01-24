using System.Security.Claims;
using Application.UseCases.Subscription.AddCredits;
using Application.UseCases.Subscription.Common;
using Application.UseCases.Subscription.ConsumeCredits;
using Application.UseCases.Subscription.CreateSubscription;
using Application.UseCases.Subscription.GetSubscription;
using Application.UseCases.Subscription.PurchasePlan;
using Application.UseCases.Subscription.UpdateAutoRenew;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class SubscriptionControllerTests
{
    private readonly SubscriptionController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public SubscriptionControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new SubscriptionController(_mediatorMock.Object);
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

    private SubscriptionDto CreateMockSubscription(Guid userId, int credits = 100, bool autoRenew = true)
    {
        return new SubscriptionDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Credits = credits,
            AutoRenew = autoRenew,
            ExpiresAt = DateTime.UtcNow.AddMonths(1)
        };
    }

    #endregion

    #region Create Subscription Tests

    [Fact]
    public async Task Create_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var credits = 100;
        var expiresAt = DateTime.UtcNow.AddMonths(1);
        var request = new CreateSubscriptionRequest(userId, credits, expiresAt);
        var subscription = CreateMockSubscription(userId, credits);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.Create(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenArgumentExceptionThrown()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(Guid.NewGuid(), 100, null);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid user ID"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Invalid user ID");
    }

    [Fact]
    public async Task Create_ReturnsServerError_WhenExceptionThrown()
    {
        // Arrange
        var request = new CreateSubscriptionRequest(Guid.NewGuid(), 100, null);
        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateSubscriptionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Unexpected error"));

        // Act
        var result = await _controller.Create(request);

        // Assert
        var statusCode = result.Should().BeOfType<ObjectResult>().Subject;
        statusCode.StatusCode.Should().Be(500);
    }

    #endregion

    #region Consume Credits Tests

    [Fact]
    public async Task Consume_ReturnsOk_WhenSufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new ConsumeCreditsRequest(userId, 10);
        var subscription = CreateMockSubscription(userId, 100);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);
        _mediatorMock.Setup(m => m.Send(It.IsAny<ConsumeCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubscriptionDto { UserId = userId, Credits = 90 });

        // Act
        var result = await _controller.Consume(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Consume_ReturnsNotFound_WhenSubscriptionNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new ConsumeCreditsRequest(userId, 10);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionDto?)null);

        // Act
        var result = await _controller.Consume(request);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Consume_ReturnsBadRequest_WhenInsufficientCredits()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new ConsumeCreditsRequest(userId, 200);
        var subscription = CreateMockSubscription(userId, 100);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.Consume(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Insufficient credits available.");
    }

    #endregion

    #region Add Credits Tests

    [Fact]
    public async Task Add_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new AddCreditsRequest(userId, 50);
        var subscription = new SubscriptionDto { UserId = userId, Credits = 150 };

        _mediatorMock.Setup(m => m.Send(It.IsAny<AddCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.Add(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Purchase Plan Tests

    [Fact]
    public async Task Purchase_ReturnsOk_WhenValidPlan()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new PurchasePlanRequest(userId, "Monthly100");
        var subscription = CreateMockSubscription(userId, 1000);

        _mediatorMock.Setup(m => m.Send(It.IsAny<PurchasePlanCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.Purchase(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Purchase_ReturnsBadRequest_WhenInvalidPlan()
    {
        // Arrange
        var request = new PurchasePlanRequest(Guid.NewGuid(), "InvalidPlan");

        // Act
        var result = await _controller.Purchase(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("Invalid plan type.");
    }

    [Fact]
    public async Task Purchase_ReturnsBadRequest_WhenInvalidOperationExceptionThrown()
    {
        // Arrange
        var request = new PurchasePlanRequest(Guid.NewGuid(), "Monthly100");
        _mediatorMock.Setup(m => m.Send(It.IsAny<PurchasePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("User already has active subscription"));

        // Act
        var result = await _controller.Purchase(request);

        // Assert
        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().Be("User already has active subscription");
    }

    #endregion

    #region Toggle Auto-Renew Tests

    [Fact]
    public async Task ToggleAutoRenew_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateAutoRenewRequest(userId, true);
        var subscription = CreateMockSubscription(userId, 100, autoRenew: true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateAutoRenewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.ToggleAutoRenew(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Update Auto-Renew Tests

    [Fact]
    public async Task UpdateAutoRenew_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new UpdateAutoRenewRequest(userId, false);
        var subscription = CreateMockSubscription(userId, 100, autoRenew: false);

        _mediatorMock.Setup(m => m.Send(It.IsAny<UpdateAutoRenewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.UpdateAutoRenew(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    #endregion

    #region Get Subscription Tests

    [Fact]
    public async Task Get_ReturnsOk_WhenSubscriptionExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var subscription = CreateMockSubscription(userId);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.Get(userId);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Get_ReturnsNotFound_WhenSubscriptionNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionDto?)null);

        // Act
        var result = await _controller.Get(userId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region Get Status Tests

    [Fact]
    public async Task GetStatus_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var subscription = CreateMockSubscription(userId);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(subscription);

        // Act
        var result = await _controller.GetStatus();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetStatus_ReturnsNotFound_WhenSubscriptionNotExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.Is<GetSubscriptionQuery>(q => q.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionDto?)null);

        // Act
        var result = await _controller.GetStatus();

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetStatus_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetStatus_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetStatus();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region Get History Tests

    [Fact]
    public async Task GetHistory_ReturnsOk_WhenUserAuthenticated()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        // Act
        var result = await _controller.GetHistory();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task GetHistory_ReturnsUnauthorized_WhenNoUserIdClaim()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GetHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetHistory_ReturnsUnauthorized_WhenInvalidGuid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GetHistory();

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion
}
