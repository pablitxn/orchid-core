using System.Net;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Application.UseCases.Auth.SyncAuth0User;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class Auth0WebhookControllerTests
{
    private readonly Auth0WebhookController _controller;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IConfiguration> _configMock;
    private readonly Mock<ILogger<Auth0WebhookController>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;

    public Auth0WebhookControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _configMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<Auth0WebhookController>>();

        _httpContext = new DefaultHttpContext();
        _httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        _controller = new Auth0WebhookController(
            _mediatorMock.Object,
            _configMock.Object,
            _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = _httpContext
            }
        };
    }

    [Fact]
    public async Task HandlePostLogin_ReturnsOk_WhenValidPayload()
    {
        // Arrange
        var webhookPayload = new
        {
            user = new
            {
                userId = "auth0|123456",
                email = "test@example.com",
                name = "Test User",
                picture = "https://example.com/avatar.png",
                permissions = new[] { "user" },
                identities = new[]
                {
                    new { connection = "google-oauth2", provider = "google-oauth2", userId = "123456" }
                }
            }
        };

        var payload = JsonSerializer.Serialize(webhookPayload);
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));

        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns((string?)null);

        _mediatorMock.Setup(m => m.Send(It.IsAny<SyncAuth0UserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Auth0SyncResult(
                UserId: Guid.NewGuid(),
                IsNewUser: true,
                WasLinked: false,
                ProfileUpdated: false,
                RolesUpdated: false));

        // Act
        var result = await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        _mediatorMock.Verify(m => m.Send(
            It.Is<SyncAuth0UserCommand>(cmd =>
                cmd.Auth0UserId == "auth0|123456" &&
                cmd.Email == "test@example.com" &&
                cmd.Name == "Test User"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePostLogin_ReturnsBadRequest_WhenInvalidPayload()
    {
        // Arrange
        var invalidPayload = "{ invalid json }";
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(invalidPayload));
        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns((string?)null);

        // Act
        var result = await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task HandlePostLogin_ReturnsBadRequest_WhenUserIsMissing()
    {
        // Arrange
        var payloadWithoutUser = "{}";
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payloadWithoutUser));
        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns((string?)null);

        // Act
        var result = await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task HandlePostLogin_ReturnsUnauthorized_WhenSignatureInvalid()
    {
        // Arrange
        var webhookPayload = new { user = new { userId = "auth0|123", email = "test@example.com" } };
        var payload = JsonSerializer.Serialize(webhookPayload);
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        _httpContext.Request.Headers["X-Auth0-Signature"] = "invalid-signature";

        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns("my-webhook-secret");

        // Act
        var result = await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task HandlePostLogin_ProcessesWebhook_WhenSignatureValid()
    {
        // Arrange
        var webhookPayload = new
        {
            user = new
            {
                userId = "auth0|validtest",
                email = "valid@example.com",
                name = "Valid User"
            }
        };

        var payload = JsonSerializer.Serialize(webhookPayload);
        var secret = "test-secret";

        // Calculate valid signature
        using var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var validSignature = "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();

        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        _httpContext.Request.Headers["X-Auth0-Signature"] = validSignature;

        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns(secret);

        _mediatorMock.Setup(m => m.Send(It.IsAny<SyncAuth0UserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Auth0SyncResult(
                UserId: Guid.NewGuid(),
                IsNewUser: false,
                WasLinked: true,
                ProfileUpdated: false,
                RolesUpdated: false));

        // Act
        var result = await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<SyncAuth0UserCommand>(cmd => cmd.Auth0UserId == "auth0|validtest"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePostLogin_ExtractsConnectionFromIdentities()
    {
        // Arrange
        var webhookPayload = new
        {
            user = new
            {
                userId = "google-oauth2|123456789",
                email = "google@example.com",
                name = "Google User",
                identities = new[]
                {
                    new { connection = "google-oauth2", provider = "google-oauth2", userId = "123456789" }
                }
            }
        };

        var payload = JsonSerializer.Serialize(webhookPayload);
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns((string?)null);

        _mediatorMock.Setup(m => m.Send(It.IsAny<SyncAuth0UserCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Auth0SyncResult(Guid.NewGuid(), true, false, false, false));

        // Act
        await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<SyncAuth0UserCommand>(cmd => cmd.Connection == "google-oauth2"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandlePostLogin_ExtractsRolesFromPermissions()
    {
        // Arrange
        var webhookPayload = new
        {
            user = new
            {
                userId = "auth0|perms123",
                email = "perms@example.com",
                permissions = new[] { "read:data", "write:data", "admin" }
            }
        };

        var payload = JsonSerializer.Serialize(webhookPayload);
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        _configMock.Setup(c => c["Auth0:WebhookSecret"]).Returns((string?)null);

        SyncAuth0UserCommand? capturedCommand = null;
        _mediatorMock.Setup(m => m.Send(It.IsAny<SyncAuth0UserCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Auth0SyncResult>, CancellationToken>((cmd, _) => capturedCommand = (SyncAuth0UserCommand)cmd)
            .ReturnsAsync(new Auth0SyncResult(Guid.NewGuid(), true, false, false, true));

        // Act
        await _controller.HandlePostLogin(CancellationToken.None);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Roles.Should().BeEquivalentTo(new[] { "read:data", "write:data", "admin" });
    }
}
