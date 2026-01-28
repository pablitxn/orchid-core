using Application.UseCases.Subscription.ProcessWebhook;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class StripeWebhookControllerTests
{
    private readonly StripeWebhookController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<StripeWebhookController>> _loggerMock;

    public StripeWebhookControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<StripeWebhookController>>();
        _controller = new StripeWebhookController(_mediatorMock.Object, _loggerMock.Object);
    }

    #region Helper Methods

    private void SetupHttpContext(string payload, string? signature)
    {
        var memoryStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(payload));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = memoryStream;

        if (signature != null)
        {
            httpContext.Request.Headers["Stripe-Signature"] = signature;
        }

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    #endregion

    #region HandleWebhook Tests

    [Fact]
    public async Task HandleWebhook_ReturnsOk_WhenWebhookProcessedSuccessfully()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\",\"id\":\"evt_123\"}";
        var signature = "t=1234567890,v1=abc123,v0=def456";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(
                It.Is<ProcessStripeWebhookCommand>(cmd =>
                    cmd.Payload == payload &&
                    cmd.Signature == signature),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(true, "evt_123"));

        // Act
        var result = await _controller.HandleWebhook();

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task HandleWebhook_ReturnsBadRequest_WhenSignatureMissing()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        SetupHttpContext(payload, null);

        // Act
        var result = await _controller.HandleWebhook();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Missing Stripe signature header.");
    }

    [Fact]
    public async Task HandleWebhook_ReturnsBadRequest_WhenSignatureEmpty()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        SetupHttpContext(payload, string.Empty);

        // Act
        var result = await _controller.HandleWebhook();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Missing Stripe signature header.");
    }

    [Fact]
    public async Task HandleWebhook_ReturnsBadRequest_WhenSignatureInvalid()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        var signature = "invalid-signature";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ProcessStripeWebhookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(false, null, "Invalid signature"));

        // Act
        var result = await _controller.HandleWebhook();

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequestResult.Value.Should().Be("Invalid signature");
    }

    [Fact]
    public async Task HandleWebhook_ReturnsOk_WhenProcessingFails_WithNonSignatureError()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        var signature = "t=1234567890,v1=abc123,v0=def456";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ProcessStripeWebhookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(false, null, "Event type not supported"));

        // Act
        var result = await _controller.HandleWebhook();

        // Assert
        // Returns 200 even on processing failure to prevent Stripe retries
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task HandleWebhook_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var payload = "{\"type\":\"invoice.payment_succeeded\",\"data\":{\"object\":{}}}";
        var signature = "t=1234567890,v1=signature_hash";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ProcessStripeWebhookCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(true, "evt_456"));

        // Act
        await _controller.HandleWebhook();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ProcessStripeWebhookCommand>(cmd =>
                cmd.Payload == payload &&
                cmd.Signature == signature),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_LogsSuccess_WhenWebhookProcessedSuccessfully()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        var signature = "t=1234567890,v1=abc123";
        var eventId = "evt_test_123";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ProcessStripeWebhookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(true, eventId));

        // Act
        await _controller.HandleWebhook();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(eventId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleWebhook_LogsWarning_WhenProcessingFails()
    {
        // Arrange
        var payload = "{\"type\":\"checkout.session.completed\"}";
        var signature = "t=1234567890,v1=abc123";
        var errorMessage = "Event processing failed";
        SetupHttpContext(payload, signature);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ProcessStripeWebhookCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookProcessingResult(false, null, errorMessage));

        // Act
        await _controller.HandleWebhook();

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains(errorMessage)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}
