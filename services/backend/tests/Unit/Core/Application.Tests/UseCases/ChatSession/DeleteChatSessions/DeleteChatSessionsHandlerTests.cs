using Application.Interfaces;
using Application.UseCases.ChatSession.DeleteChatSessions;
using Moq;

namespace Application.Tests.UseCases.ChatSession.DeleteChatSessions;

public class DeleteChatSessionsHandlerTests
{
    private readonly DeleteChatSessionsHandler _handler;
    private readonly Mock<IChatSessionRepository> _repo = new();

    public DeleteChatSessionsHandlerTests()
    {
        _handler = new DeleteChatSessionsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_CallsRepository()
    {
        var ids = new[] { Guid.NewGuid() };
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(ids, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_UsesCancellationToken()
    {
        var token = new CancellationTokenSource();
        await _handler.Handle(new DeleteChatSessionsCommand(Array.Empty<Guid>()), token.Token);
        _repo.Verify(r => r.DeleteManyAsync(It.IsAny<IEnumerable<Guid>>(), token.Token));
    }

    [Fact]
    public async Task Handle_AllowsEmptyIds()
    {
        await _handler.Handle(new DeleteChatSessionsCommand(Array.Empty<Guid>()), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(Array.Empty<Guid>(), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_MultipleIds()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(ids, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_DifferentCalls()
    {
        var ids1 = new[] { Guid.NewGuid() };
        var ids2 = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await _handler.Handle(new DeleteChatSessionsCommand(ids1), CancellationToken.None);
        await _handler.Handle(new DeleteChatSessionsCommand(ids2), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(ids1, It.IsAny<CancellationToken>()));
        _repo.Verify(r => r.DeleteManyAsync(ids2, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_CompletesTask()
    {
        await _handler.Handle(new DeleteChatSessionsCommand(Array.Empty<Guid>()), CancellationToken.None);
        Assert.True(true);
    }

    [Fact]
    public async Task Handle_PassesIdsCorrectly()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(It.Is<IEnumerable<Guid>>(g => g.SequenceEqual(ids)), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_CanBeCalledRepeatedly()
    {
        var ids = new[] { Guid.NewGuid() };
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(ids, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_AllowsLargeIdList()
    {
        var ids = Enumerable.Range(0, 10).Select(_ => Guid.NewGuid()).ToArray();
        await _handler.Handle(new DeleteChatSessionsCommand(ids), CancellationToken.None);
        _repo.Verify(r => r.DeleteManyAsync(ids, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_NoSideEffects()
    {
        await _handler.Handle(new DeleteChatSessionsCommand(new[] { Guid.NewGuid() }), CancellationToken.None);
        Assert.True(true);
    }
}
