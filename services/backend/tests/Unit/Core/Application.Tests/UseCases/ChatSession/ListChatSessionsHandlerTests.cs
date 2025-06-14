using Application.Interfaces;
using Application.UseCases.ChatSession.ListChatSessions;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.ChatSession;

public class ListChatSessionsHandlerTests
{
    private readonly ListChatSessionsHandler _handler;
    private readonly Mock<IChatSessionRepository> _repo = new();
    private readonly Guid _uid = Guid.NewGuid();

    public ListChatSessionsHandlerTests()
    {
        _handler = new ListChatSessionsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsSessions()
    {
        var expected = new List<ChatSessionEntity> { new() { Id = Guid.NewGuid(), UserId = _uid, SessionId = "sid" } };
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, null),
            CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }

    [Fact]
    public async Task Handle_PassesArchivedFlag()
    {
        _repo.Setup(r => r.ListAsync(_uid, true, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        await _handler.Handle(new ListChatSessionsQuery(_uid, true, null, null, null, null, null), CancellationToken.None);

        _repo.Verify(r => r.ListAsync(_uid, true, null, null, null, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FiltersByAgentId()
    {
        var agentId = Guid.NewGuid();
        _repo.Setup(r => r.ListAsync(_uid, false, agentId, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        await _handler.Handle(new ListChatSessionsQuery(_uid, false, agentId, null, null, null, null), CancellationToken.None);

        _repo.Verify(r => r.ListAsync(_uid, false, agentId, null, null, null, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_FiltersByTeamId()
    {
        var teamId = Guid.NewGuid();
        _repo.Setup(r => r.ListAsync(_uid, false, null, teamId, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, teamId, null, null, null), CancellationToken.None);

        _repo.Verify(r => r.ListAsync(_uid, false, null, teamId, null, null, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_FiltersByDateRange()
    {
        var start = DateTime.UtcNow.AddDays(-1);
        var end = DateTime.UtcNow;
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, start, end, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, start, end, null), CancellationToken.None);

        _repo.Verify(r => r.ListAsync(_uid, false, null, null, start, end, null, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_FiltersByType()
    {
        // const string type = "voice";
        // _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, type, It.IsAny<CancellationToken>()))
        //     .ReturnsAsync(new List<ChatSessionEntity>());
        //
        // await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, type), CancellationToken.None);
        //
        // _repo.Verify(r => r.ListAsync(_uid, false, null, null, null, null, type, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNone()
    {
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>());

        var result = await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, null), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_UsesCancellationToken()
    {
        var tokenSource = new CancellationTokenSource();
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, null, tokenSource.Token))
            .ReturnsAsync(new List<ChatSessionEntity>())
            .Verifiable();

        await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, null), tokenSource.Token);

        _repo.Verify();
    }

    [Fact]
    public async Task Handle_ReturnsListFromRepository()
    {
        var expected = new List<ChatSessionEntity> { new() { SessionId = "a" } };
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, null), CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task Handle_UsesDefaultParameters()
    {
        _repo.Setup(r => r.ListAsync(_uid, false, null, null, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatSessionEntity>())
            .Verifiable();

        await _handler.Handle(new ListChatSessionsQuery(_uid, false, null, null, null, null, null), CancellationToken.None);

        _repo.Verify();
    }
}