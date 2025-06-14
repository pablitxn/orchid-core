using Application.Interfaces;
using Application.UseCases.ChatSession.CreateChatSession;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.ChatSession;

public class CreateChatSessionHandlerTests
{
    private readonly CreateChatSessionHandler _handler;
    private readonly Mock<IChatSessionRepository> _repo = new();
    private readonly DateTime _before;

    public CreateChatSessionHandlerTests()
    {
        _handler = new CreateChatSessionHandler(_repo.Object);
        _before = DateTime.UtcNow;
    }

    [Fact]
    public async Task Handle_CreatesSession()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(It.IsAny<ChatSessionEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(cmd.SessionId, result.SessionId);
    }

    [Fact]
    public async Task Handle_SetsUserId()
    {
        var userId = Guid.NewGuid();
        var cmd = new CreateChatSessionCommand(userId, Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(userId, result.UserId);
    }

    [Fact]
    public async Task Handle_SetsTitle()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), "title");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("title", result.Title);
    }

    [Fact]
    public async Task Handle_DefaultsIsArchivedFalse()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.False(result.IsArchived);
    }

    [Fact]
    public async Task Handle_SetsTimestamps()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.CreatedAt >= _before);
        Assert.True(result.UpdatedAt >= _before);
    }

    [Fact]
    public async Task Handle_GeneratesId()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Handle_CallsRepositoryWithCancellationToken()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);
        var tokenSource = new CancellationTokenSource();
        _repo.Setup(r => r.CreateAsync(It.IsAny<ChatSessionEntity>(), tokenSource.Token))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _handler.Handle(cmd, tokenSource.Token);

        _repo.Verify();
    }

    [Fact]
    public async Task Handle_ReturnsEntityPassedToRepo()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);
        ChatSessionEntity? saved = null;
        _repo.Setup(r => r.CreateAsync(It.IsAny<ChatSessionEntity>(), It.IsAny<CancellationToken>()))
            .Callback<ChatSessionEntity, CancellationToken>((e, _) => saved = e)
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(saved, result);
    }

    [Fact]
    public async Task Handle_AllowsNullTitle()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Null(result.Title);
    }

    [Fact]
    public async Task Handle_SetsUpdatedAtEqualToCreatedAt()
    {
        var cmd = new CreateChatSessionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid().ToString(), null);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(result.CreatedAt, result.UpdatedAt);
    }
}