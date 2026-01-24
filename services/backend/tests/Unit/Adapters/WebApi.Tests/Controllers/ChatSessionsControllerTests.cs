using System.Security.Claims;
using Application.UseCases.ChatSession.ArchiveChatSession;
using Application.UseCases.ChatSession.Common;
using Application.UseCases.ChatSession.CreateChatSession;
using Application.UseCases.ChatSession.DeleteChatSessions;
using Application.UseCases.ChatSession.GetChatSessionById;
using Application.UseCases.ChatSession.ListChatSessions;
using Domain.Enums;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class ChatSessionsControllerTests
{
    private readonly ChatSessionsController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public ChatSessionsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new ChatSessionsController(_mediatorMock.Object);
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

    private void SetupAuthenticatedUserWithSubClaim(Guid userId)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString())
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

    #region List Tests

    [Fact]
    public async Task List_ThrowsUnauthorizedAccessException_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();

        // Act
        var act = () => _controller.List();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    [Fact]
    public async Task List_ThrowsUnauthorizedAccessException_WhenUserIdClaimInvalid()
    {
        // Arrange
        SetupUserWithInvalidGuid();

        // Act
        var act = () => _controller.List();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("User ID claim not found or invalid in token.");
    }

    [Fact]
    public async Task List_ReturnsOk_WithChatSessions_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var expectedSessions = new List<ChatSessionDto>
        {
            new() { Id = Guid.NewGuid(), SessionId = "session-1", UserId = userId },
            new() { Id = Guid.NewGuid(), SessionId = "session-2", UserId = userId }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListChatSessionsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _controller.List();

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<ChatSessionDto>>().Subject;
        sessions.Should().HaveCount(2);
        sessions[0].SessionId.Should().Be("session-1");
    }

    [Fact]
    public async Task List_UsesSubClaim_WhenNameIdentifierNotPresent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUserWithSubClaim(userId);

        var expectedSessions = new List<ChatSessionDto>
        {
            new() { Id = Guid.NewGuid(), SessionId = "session-1", UserId = userId }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListChatSessionsQuery>(q => q.UserId == userId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSessions);

        // Act
        var result = await _controller.List();

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListChatSessionsQuery>(q => q.UserId == userId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_SendsCorrectQuery_WithDefaultParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListChatSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionDto>());

        // Act
        await _controller.List();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListChatSessionsQuery>(q =>
                q.UserId == userId &&
                q.Archived == false &&
                q.AgentId == null &&
                q.TeamId == null &&
                q.StartDate == null &&
                q.EndDate == null &&
                q.Type == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_SendsCorrectQuery_WithAllParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;
        var type = InteractionType.Text;

        SetupAuthenticatedUser(userId);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ListChatSessionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionDto>());

        // Act
        await _controller.List(archived: true, agentId: agentId, teamId: teamId, startDate: startDate, endDate: endDate, type: type);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ListChatSessionsQuery>(q =>
                q.UserId == userId &&
                q.Archived == true &&
                q.AgentId == agentId &&
                q.TeamId == teamId &&
                q.StartDate == startDate &&
                q.EndDate == endDate &&
                q.Type == type),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task List_FiltersArchivedSessions_WhenArchivedIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        var archivedSessions = new List<ChatSessionDto>
        {
            new() { Id = Guid.NewGuid(), SessionId = "archived-1", UserId = userId, IsArchived = true }
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<ListChatSessionsQuery>(q => q.UserId == userId && q.Archived == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(archivedSessions);

        // Act
        var result = await _controller.List(archived: true);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var sessions = okResult.Value.Should().BeAssignableTo<List<ChatSessionDto>>().Subject;
        sessions.Should().HaveCount(1);
        sessions[0].IsArchived.Should().BeTrue();
    }

    #endregion

    #region GetBySessionId Tests

    [Fact]
    public async Task GetBySessionId_ReturnsOk_WithChatSession_WhenFound()
    {
        // Arrange
        var sessionId = "test-session-123";
        var expectedSession = new GetChatSessionByIdDto(
            Guid.NewGuid(),
            sessionId,
            Guid.NewGuid(),
            null,
            null,
            "Test Session",
            "Chat",
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null
        );

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetChatSessionByIdQuery>(q => q.SessionId == sessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _controller.GetBySessionId(sessionId);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var session = okResult.Value.Should().BeOfType<GetChatSessionByIdDto>().Subject;
        session.SessionId.Should().Be(sessionId);
    }

    [Fact]
    public async Task GetBySessionId_ReturnsNotFound_WhenSessionDoesNotExist()
    {
        // Arrange
        var sessionId = "non-existent-session";

        _mediatorMock.Setup(m => m.Send(
                It.Is<GetChatSessionByIdQuery>(q => q.SessionId == sessionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetChatSessionByIdDto?)null);

        // Act
        var result = await _controller.GetBySessionId(sessionId);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBySessionId_SendsCorrectQuery_ToMediator()
    {
        // Arrange
        var sessionId = "test-session-456";
        var expectedDto = new GetChatSessionByIdDto(
            Guid.NewGuid(),
            sessionId,
            Guid.NewGuid(),
            null,
            null,
            "Test",
            "Chat",
            DateTime.UtcNow,
            DateTime.UtcNow,
            null,
            null
        );
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetChatSessionByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        await _controller.GetBySessionId(sessionId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetChatSessionByIdQuery>(q => q.SessionId == sessionId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_ReturnsOk_WithCreatedSession_WhenSuccessful()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var command = new CreateChatSessionCommand(userId, agentId, "test-session", "Test Session");

        var expectedSession = new ChatSessionDto
        {
            Id = Guid.NewGuid(),
            SessionId = "new-session-123",
            UserId = userId,
            AgentId = agentId
        };

        _mediatorMock.Setup(m => m.Send(
                It.Is<CreateChatSessionCommand>(c =>
                    c.UserId == userId &&
                    c.AgentId == agentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedSession);

        // Act
        var result = await _controller.Create(command);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var session = okResult.Value.Should().BeOfType<ChatSessionDto>().Subject;
        session.UserId.Should().Be(userId);
        session.AgentId.Should().Be(agentId);
    }

    [Fact]
    public async Task Create_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var sessionId = "test-123";
        var title = "Test Title";
        var command = new CreateChatSessionCommand(userId, agentId, sessionId, title);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateChatSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatSessionDto { Id = Guid.NewGuid(), SessionId = "test" });

        // Act
        await _controller.Create(command);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreateChatSessionCommand>(c =>
                c.UserId == userId &&
                c.AgentId == agentId &&
                c.SessionId == sessionId &&
                c.Title == title),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_HandlesAllCommandParameters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var sessionId = "test-session-id";
        var title = "Test Session Title";

        var command = new CreateChatSessionCommand(userId, agentId, sessionId, title);

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateChatSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatSessionDto { Id = Guid.NewGuid(), SessionId = sessionId });

        // Act
        var result = await _controller.Create(command);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<CreateChatSessionCommand>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Archive Tests

    [Fact]
    public async Task Archive_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ArchiveChatSessionRequest(true);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<ArchiveChatSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Archive(sessionId, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Archive_SendsCorrectCommand_ToArchive()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ArchiveChatSessionRequest(true);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ArchiveChatSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Archive(sessionId, request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ArchiveChatSessionCommand>(c =>
                c.Id == sessionId &&
                c.Archived == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Archive_SendsCorrectCommand_ToUnarchive()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var request = new ArchiveChatSessionRequest(false);

        _mediatorMock.Setup(m => m.Send(It.IsAny<ArchiveChatSessionCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Archive(sessionId, request);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<ArchiveChatSessionCommand>(c =>
                c.Id == sessionId &&
                c.Archived == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task Delete_ReturnsNoContent_WhenSuccessful()
    {
        // Arrange
        var sessionIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var command = new DeleteChatSessionsCommand(sessionIds);

        _mediatorMock.Setup(m => m.Send(
                It.IsAny<DeleteChatSessionsCommand>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(command);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_SendsCorrectCommand_ToMediator()
    {
        // Arrange
        var sessionIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var command = new DeleteChatSessionsCommand(sessionIds);

        _mediatorMock.Setup(m => m.Send(It.IsAny<DeleteChatSessionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Delete(command);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteChatSessionsCommand>(c =>
                c.Ids.SequenceEqual(sessionIds)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_HandlesSingleSession()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var command = new DeleteChatSessionsCommand(new[] { sessionId });

        _mediatorMock.Setup(m => m.Send(It.IsAny<DeleteChatSessionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(command);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteChatSessionsCommand>(c => c.Ids.Count() == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_HandlesMultipleSessions()
    {
        // Arrange
        var sessionIds = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        var command = new DeleteChatSessionsCommand(sessionIds);

        _mediatorMock.Setup(m => m.Send(It.IsAny<DeleteChatSessionsCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(command);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        _mediatorMock.Verify(m => m.Send(
            It.Is<DeleteChatSessionsCommand>(c => c.Ids.Count() == 10),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
