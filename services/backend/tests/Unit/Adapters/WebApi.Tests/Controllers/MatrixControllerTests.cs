using System.Security.Claims;
using Application.UseCases.Matrix.GenerateLinkCode;
using Application.UseCases.Matrix.GetUserLink;
using Application.UseCases.Matrix.SendMessage;
using Application.UseCases.Matrix.ValidateLinkCode;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class MatrixControllerTests
{
    private readonly MatrixController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public MatrixControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new MatrixController(_mediatorMock.Object);
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

    #endregion

    #region GenerateLinkCode Tests

    [Fact]
    public async Task GenerateLinkCode_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var code = "ABC123";
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        _mediatorMock.Setup(m => m.Send(
                It.Is<GenerateMatrixLinkCodeCommand>(cmd => cmd.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenerateMatrixLinkCodeDto.Success(code, expiresAt));

        // Act
        var result = await _controller.GenerateLinkCode(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GenerateLinkCodeResponse>().Subject;
        response.Code.Should().Be(code);
        response.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GenerateLinkCode_ReturnsBadRequest_WhenAlreadyLinked()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var matrixUserId = "@user:matrix.example.com";

        _mediatorMock.Setup(m => m.Send(
                It.Is<GenerateMatrixLinkCodeCommand>(cmd => cmd.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GenerateMatrixLinkCodeDto.AlreadyLinked(matrixUserId));

        // Act
        var result = await _controller.GenerateLinkCode(CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var response = badRequestResult.Value.Should().BeOfType<AlreadyLinkedResponse>().Subject;
        response.Error.Should().Be("ALREADY_LINKED");
        response.MatrixUserId.Should().Be(matrixUserId);
    }

    [Fact]
    public async Task GenerateLinkCode_ReturnsUnauthorized_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var result = await _controller.GenerateLinkCode(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GenerateLinkCode_ReturnsUnauthorized_WhenUserIdInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var result = await _controller.GenerateLinkCode(CancellationToken.None);

        // Assert
        result.Should().BeOfType<UnauthorizedResult>();
    }

    #endregion

    #region ValidateLinkCode Tests

    [Fact]
    public async Task ValidateLinkCode_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid()); // Service account authentication
        var request = new ValidateLinkCodeRequest("@user:matrix.example.com", "ABC123");
        var linkedUserId = Guid.NewGuid();
        var userName = "TestUser";

        _mediatorMock.Setup(m => m.Send(
                It.Is<ValidateMatrixLinkCodeCommand>(cmd =>
                    cmd.MatrixUserId == request.MatrixUserId &&
                    cmd.Code == request.Code),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidateMatrixLinkCodeDto.Success(linkedUserId, userName));

        // Act
        var result = await _controller.ValidateLinkCode(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ValidateLinkCodeResponse>().Subject;
        response.Success.Should().BeTrue();
        response.UserId.Should().Be(linkedUserId);
        response.UserName.Should().Be(userName);
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsOk_WithError_WhenCodeInvalid()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var request = new ValidateLinkCodeRequest("@user:matrix.example.com", "WRONGCODE");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ValidateMatrixLinkCodeCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidateMatrixLinkCodeDto.InvalidCode());

        // Act
        var result = await _controller.ValidateLinkCode(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ValidateLinkCodeResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("INVALID_CODE");
    }

    [Fact]
    public async Task ValidateLinkCode_ReturnsOk_WithError_WhenCodeExpired()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var request = new ValidateLinkCodeRequest("@user:matrix.example.com", "EXPIRED");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ValidateMatrixLinkCodeCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ValidateMatrixLinkCodeDto.ExpiredCode());

        // Act
        var result = await _controller.ValidateLinkCode(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ValidateLinkCodeResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("EXPIRED_CODE");
    }

    #endregion

    #region GetUserLink Tests

    [Fact]
    public async Task GetUserLink_ReturnsOk_WhenUserIsLinked()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var matrixUserId = "@user:matrix.example.com";
        var linkedUserId = Guid.NewGuid();
        var userName = "TestUser";

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetMatrixUserLinkQuery>(q => q.MatrixUserId == matrixUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMatrixUserLinkDto.LinkedTo(linkedUserId, userName));

        // Act
        var result = await _controller.GetUserLink(matrixUserId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GetUserLinkResponse>().Subject;
        response.Linked.Should().BeTrue();
        response.UserId.Should().Be(linkedUserId);
        response.UserName.Should().Be(userName);
    }

    [Fact]
    public async Task GetUserLink_ReturnsOk_WhenUserIsNotLinked()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var matrixUserId = "@unlinked:matrix.example.com";

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetMatrixUserLinkQuery>(q => q.MatrixUserId == matrixUserId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetMatrixUserLinkDto.NotLinked());

        // Act
        var result = await _controller.GetUserLink(matrixUserId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<GetUserLinkResponse>().Subject;
        response.Linked.Should().BeFalse();
        response.UserId.Should().BeNull();
        response.UserName.Should().BeNull();
    }

    #endregion

    #region SendMessage Tests

    [Fact]
    public async Task SendMessage_ReturnsOk_WhenSuccessful()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var request = new SendMatrixMessageRequest(
            "@user:matrix.example.com",
            agentId,
            "!room:matrix.example.com",
            "Hello, agent!");

        _mediatorMock.Setup(m => m.Send(
                It.Is<SendMatrixMessageCommand>(cmd =>
                    cmd.MatrixUserId == request.MatrixUserId &&
                    cmd.AgentId == request.AgentId &&
                    cmd.RoomId == request.RoomId &&
                    cmd.Content == request.Content),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendMatrixMessageDto.Succeeded(
                "Hi! How can I help you?",
                "session-123",
                10,
                90));

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SendMatrixMessageResponse>().Subject;
        response.Success.Should().BeTrue();
        response.Response.Should().Be("Hi! How can I help you?");
        response.SessionId.Should().Be("session-123");
        response.CreditsUsed.Should().Be(10);
        response.CreditsRemaining.Should().Be(90);
    }

    [Fact]
    public async Task SendMessage_Returns429_WhenRateLimited()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var request = new SendMatrixMessageRequest(
            "@user:matrix.example.com",
            agentId,
            "!room:matrix.example.com",
            "Hello!");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<SendMatrixMessageCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendMatrixMessageDto.RateLimited(60));

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var statusCodeResult = result.Should().BeOfType<ObjectResult>().Subject;
        statusCodeResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        var response = statusCodeResult.Value.Should().BeOfType<SendMatrixMessageResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("RATE_LIMITED");
        response.RetryAfter.Should().Be(60);
    }

    [Fact]
    public async Task SendMessage_ReturnsOk_WithError_WhenUserNotLinked()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var request = new SendMatrixMessageRequest(
            "@unlinked:matrix.example.com",
            agentId,
            "!room:matrix.example.com",
            "Hello!");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<SendMatrixMessageCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendMatrixMessageDto.UserNotLinked());

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SendMatrixMessageResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("USER_NOT_LINKED");
    }

    [Fact]
    public async Task SendMessage_ReturnsOk_WithError_WhenInsufficientCredits()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var request = new SendMatrixMessageRequest(
            "@user:matrix.example.com",
            agentId,
            "!room:matrix.example.com",
            "Hello!");

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<SendMatrixMessageCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendMatrixMessageDto.InsufficientCredits(5));

        // Act
        var result = await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<SendMatrixMessageResponse>().Subject;
        response.Success.Should().BeFalse();
        response.Error.Should().Be("INSUFFICIENT_CREDITS");
        response.CreditsRemaining.Should().Be(5);
    }

    [Fact]
    public async Task SendMessage_SendsCorrectCommand_WithOptionalParameters()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var agentId = Guid.NewGuid();
        var request = new SendMatrixMessageRequest(
            "@user:matrix.example.com",
            agentId,
            "!room:matrix.example.com",
            "Hello!",
            "$event123",
            "mention");

        _mediatorMock.Setup(m => m.Send(It.IsAny<SendMatrixMessageCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SendMatrixMessageDto.Succeeded("Response", "session-123", 10, 90));

        // Act
        await _controller.SendMessage(request, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<SendMatrixMessageCommand>(cmd =>
                cmd.MatrixUserId == request.MatrixUserId &&
                cmd.AgentId == request.AgentId &&
                cmd.RoomId == request.RoomId &&
                cmd.Content == request.Content &&
                cmd.ReplyToEventId == request.ReplyToEventId &&
                cmd.TriggerType == request.TriggerType),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
