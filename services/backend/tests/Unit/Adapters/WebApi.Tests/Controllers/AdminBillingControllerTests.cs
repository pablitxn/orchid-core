using System.Security.Claims;
using Application.UseCases.AdminBilling.AdjustCredits;
using Application.UseCases.AdminBilling.Common;
using Application.UseCases.AdminBilling.GetAllSubscriptions;
using Application.UseCases.AdminBilling.GetSubscriptionByUserId;
using Application.UseCases.AdminBilling.GetWebhookLogs;
using Application.UseCases.AdminBilling.RetryWebhook;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class AdminBillingControllerTests
{
    private readonly AdminBillingController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<AdminBillingController>> _loggerMock;

    public AdminBillingControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<AdminBillingController>>();
        _controller = new AdminBillingController(_mediatorMock.Object, _loggerMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedAdmin(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, "admin")
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

    #region GetAllSubscriptions Tests

    [Fact]
    public async Task GetAllSubscriptions_ReturnsOk_WithPaginatedList()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);
        var expectedResult = new GetAllSubscriptionsDto
        {
            Items = new List<AdminSubscriptionDto>
            {
                new AdminSubscriptionDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 100 },
                new AdminSubscriptionDto { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Credits = 500 }
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 50
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetAllSubscriptionsQuery>(q => q.Page == 1 && q.PageSize == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetAllSubscriptions();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var subscriptions = okResult.Value.Should().BeOfType<GetAllSubscriptionsDto>().Subject;
        subscriptions.Items.Should().HaveCount(2);
        subscriptions.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllSubscriptions_SendsCorrectPaginationParameters()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);
        var page = 3;
        var pageSize = 25;

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetAllSubscriptionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetAllSubscriptionsDto { Items = [], TotalCount = 0, Page = page, PageSize = pageSize });

        // Act
        await _controller.GetAllSubscriptions(page, pageSize);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetAllSubscriptionsQuery>(q => q.Page == page && q.PageSize == pageSize),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllSubscriptions_Returns500_WhenExceptionOccurs()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetAllSubscriptionsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.GetAllSubscriptions();

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(500);
    }

    #endregion

    #region GetSubscriptionByUserId Tests

    [Fact]
    public async Task GetSubscriptionByUserId_ReturnsOk_WhenSubscriptionFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var expectedSubscription = new AdminSubscriptionDto
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Credits = 100,
            Status = SubscriptionStatus.Active
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetSubscriptionByUserIdQuery>(q => q.UserId == targetUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSubscription);

        // Act
        var result = await _controller.GetSubscriptionByUserId(targetUserId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var subscription = okResult.Value.Should().BeOfType<AdminSubscriptionDto>().Subject;
        subscription.UserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task GetSubscriptionByUserId_ReturnsNotFound_WhenSubscriptionNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetSubscriptionByUserIdQuery>(q => q.UserId == targetUserId),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(targetUserId));

        // Act
        var result = await _controller.GetSubscriptionByUserId(targetUserId);

        // Assert
        var notFoundResult = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        notFoundResult.Value.Should().NotBeNull();
    }

    #endregion

    #region AdjustCredits Tests

    [Fact]
    public async Task AdjustCredits_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new AdjustCreditsRequest(targetUserId, 100, "Compensation for service outage");

        var expectedResult = new AdminSubscriptionDto
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            Credits = 200
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<AdjustCreditsCommand>(cmd =>
                    cmd.UserId == targetUserId &&
                    cmd.Amount == 100 &&
                    cmd.Reason == request.Reason &&
                    cmd.AdminUserId == adminId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.AdjustCredits(request);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var subscription = okResult.Value.Should().BeOfType<AdminSubscriptionDto>().Subject;
        subscription.Credits.Should().Be(200);
    }

    [Fact]
    public async Task AdjustCredits_ReturnsUnauthorized_WhenAdminNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var request = new AdjustCreditsRequest(Guid.NewGuid(), 100, "Test");

        // Act
        var result = await _controller.AdjustCredits(request);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task AdjustCredits_ReturnsNotFound_WhenSubscriptionNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new AdjustCreditsRequest(targetUserId, 100, "Test");

        _mediatorMock.Setup(m => m.Send(It.IsAny<AdjustCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SubscriptionNotFoundException(targetUserId));

        // Act
        var result = await _controller.AdjustCredits(request);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task AdjustCredits_ReturnsBadRequest_WhenValidationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new AdjustCreditsRequest(Guid.NewGuid(), 0, "");

        _mediatorMock.Setup(m => m.Send(It.IsAny<AdjustCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Amount cannot be zero"));

        // Act
        var result = await _controller.AdjustCredits(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task AdjustCredits_ReturnsBadRequest_WhenInsufficientCredits()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var request = new AdjustCreditsRequest(Guid.NewGuid(), -1000, "Negative adjustment");

        _mediatorMock.Setup(m => m.Send(It.IsAny<AdjustCreditsCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InsufficientCreditsException(1000, 50));

        // Act
        var result = await _controller.AdjustCredits(request);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion

    #region GetWebhookLogs Tests

    [Fact]
    public async Task GetWebhookLogs_ReturnsOk_WithPaginatedList()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var expectedResult = new GetWebhookLogsDto
        {
            Items = new List<WebhookLogDto>
            {
                new WebhookLogDto { Id = Guid.NewGuid(), StripeEventId = "evt_1", EventType = "checkout.session.completed", Status = WebhookEventStatus.Processed },
                new WebhookLogDto { Id = Guid.NewGuid(), StripeEventId = "evt_2", EventType = "invoice.payment_succeeded", Status = WebhookEventStatus.Failed }
            },
            TotalCount = 2,
            Page = 1,
            PageSize = 50,
            Stats = new WebhookStatsDto { TotalEvents = 100, ProcessedEvents = 95, FailedEvents = 5 }
        };

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetWebhookLogsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetWebhookLogs();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var logs = okResult.Value.Should().BeOfType<GetWebhookLogsDto>().Subject;
        logs.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetWebhookLogs_SendsCorrectFilterParameters()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var status = WebhookEventStatus.Failed;
        var eventType = "checkout.session.completed";
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var page = 2;
        var pageSize = 25;

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetWebhookLogsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GetWebhookLogsDto { Items = [], TotalCount = 0, Page = page, PageSize = pageSize });

        // Act
        await _controller.GetWebhookLogs(status, eventType, startDate, endDate, page, pageSize);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetWebhookLogsQuery>(q =>
                q.Status == status &&
                q.EventType == eventType &&
                q.StartDate == startDate &&
                q.EndDate == endDate &&
                q.Page == page &&
                q.PageSize == pageSize),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region RetryWebhook Tests

    [Fact]
    public async Task RetryWebhook_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        var expectedResult = new RetryWebhookDto
        {
            Id = webhookId,
            StripeEventId = "evt_test",
            Success = true,
            Message = "Webhook reprocessed successfully"
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<RetryWebhookCommand>(cmd =>
                    cmd.WebhookEventId == webhookId &&
                    cmd.AdminUserId == adminId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.RetryWebhook(webhookId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var retryResult = okResult.Value.Should().BeOfType<RetryWebhookDto>().Subject;
        retryResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RetryWebhook_ReturnsUnauthorized_WhenAdminNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.RetryWebhook(Guid.NewGuid());

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task RetryWebhook_ReturnsNotFound_WhenWebhookNotFound()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<RetryWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new EntityNotFoundException("Webhook event", webhookId));

        // Act
        var result = await _controller.RetryWebhook(webhookId);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task RetryWebhook_ReturnsBadRequest_WhenOperationFails()
    {
        // Arrange
        var adminId = Guid.NewGuid();
        var webhookId = Guid.NewGuid();
        SetupAuthenticatedAdmin(adminId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<RetryWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationFailedException("Max retry count exceeded"));

        // Act
        var result = await _controller.RetryWebhook(webhookId);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    #endregion
}
