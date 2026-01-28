using System.Security.Claims;
using Application.UseCases.ChatV2.Common;
using Application.UseCases.ChatV2.ListMessages;
using Application.UseCases.ChatV2.SendMessage;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers.V2;
using WebApi.Models.V2;

namespace WebApi.Tests.Controllers.V2;

public class ChatMessagesControllerTests
{
    private readonly ChatMessagesController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public ChatMessagesControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new ChatMessagesController(_mediatorMock.Object);
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

    #region ListMessages Tests

    [Fact]
    public async Task ListMessages_ReturnsOk_WithPaginatedMessages_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session-123";
        SetupAuthenticatedUser(userId);

        var expectedMessages = new PaginatedMessagesDto
        {
            Messages = new List<MessageV2Dto>
            {
                new MessageV2Dto { Id = Guid.NewGuid().ToString(), Role = "user", Content = "Hello", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() },
                new MessageV2Dto { Id = Guid.NewGuid().ToString(), Role = "assistant", Content = "Hi there!", Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }
            },
            HasMore = false,
            TotalCount = 2,
            NextCursor = null,
            PreviousCursor = null
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListMessagesV2Query>(q =>
                    q.SessionId == sessionId &&
                    q.UserId == userId &&
                    q.Limit == 50 &&
                    q.Cursor == null &&
                    q.Direction == "backward"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessages);

        // Act
        var result = await _controller.ListMessages(sessionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = okResult.Value.Should().BeOfType<PaginatedMessagesDto>().Subject;
        messages.Messages.Should().HaveCount(2);
        messages.TotalCount.Should().Be(2);
        messages.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task ListMessages_SendsCorrectQueryParameters_WithCustomValues()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session-456";
        var cursor = "encoded-cursor";
        var limit = 25;
        var direction = "forward";
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListMessagesV2Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PaginatedMessagesDto
            {
                Messages = new List<MessageV2Dto>(),
                HasMore = false,
                TotalCount = 0
            });

        // Act
        await _controller.ListMessages(sessionId, limit, cursor, direction);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListMessagesV2Query>(q =>
                q.SessionId == sessionId &&
                q.UserId == userId &&
                q.Limit == limit &&
                q.Cursor == cursor &&
                q.Direction == direction),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListMessages_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var sessionId = "test-session";

        // Act
        var act = async () => await _controller.ListMessages(sessionId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    [Fact]
    public async Task ListMessages_ThrowsUnauthorizedAccessException_WhenUserIdInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var sessionId = "test-session";

        // Act
        var act = async () => await _controller.ListMessages(sessionId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    #endregion

    #region SendMessage Tests

    [Fact]
    public async Task SendMessage_ReturnsCreatedAtAction_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session-789";
        SetupAuthenticatedUser(userId);

        var request = new SendMessageV2Request("Hello, assistant!");

        var expectedMessage = new MessageV2Dto
        {
            Id = Guid.NewGuid().ToString(),
            Role = "user",
            Content = request.Content,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<SendMessageV2Command>(cmd =>
                    cmd.SessionId == sessionId &&
                    cmd.UserId == userId &&
                    cmd.Content == request.Content &&
                    cmd.Artifacts == null),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMessage);

        // Act
        var result = await _controller.SendMessage(sessionId, request);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(ChatMessagesController.ListMessages));
        createdResult.RouteValues.Should().ContainKey("sessionId").WhoseValue.Should().Be(sessionId);
        var message = createdResult.Value.Should().BeOfType<MessageV2Dto>().Subject;
        message.Content.Should().Be(request.Content);
    }

    [Fact]
    public async Task SendMessage_SendsCorrectCommand_WithArtifacts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = "test-session-artifacts";
        SetupAuthenticatedUser(userId);

        var artifacts = new List<ArtifactDto>
        {
            new ArtifactDto { Id = "artifact-1", Type = "code", Title = "Test Code", Content = "console.log('test');", Language = "javascript" }
        };
        var request = new SendMessageV2Request("Check this code", artifacts);

        _mediatorMock.Setup(m => m.Send(It.IsAny<SendMessageV2Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessageV2Dto
            {
                Id = Guid.NewGuid().ToString(),
                Role = "user",
                Content = request.Content,
                Artifacts = artifacts,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

        // Act
        await _controller.SendMessage(sessionId, request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<SendMessageV2Command>(cmd =>
                cmd.SessionId == sessionId &&
                cmd.UserId == userId &&
                cmd.Content == request.Content &&
                cmd.Artifacts != null &&
                cmd.Artifacts.Count == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendMessage_ThrowsUnauthorizedAccessException_WhenUserNotAuthenticated()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var sessionId = "test-session";
        var request = new SendMessageV2Request("Hello");

        // Act
        var act = async () => await _controller.SendMessage(sessionId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    [Fact]
    public async Task SendMessage_ThrowsUnauthorizedAccessException_WhenUserIdInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();
        var sessionId = "test-session";
        var request = new SendMessageV2Request("Hello");

        // Act
        var act = async () => await _controller.SendMessage(sessionId, request);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    #endregion
}
