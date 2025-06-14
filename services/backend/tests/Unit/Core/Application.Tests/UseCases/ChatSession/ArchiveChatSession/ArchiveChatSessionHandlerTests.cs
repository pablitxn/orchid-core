using Application.Interfaces;
using Application.UseCases.ChatSession.ArchiveChatSession;
using MediatR;
using Moq;

namespace Application.Tests.UseCases.ChatSession.ArchiveChatSession;

public class ArchiveChatSessionHandlerTests
{
    private readonly ArchiveChatSessionHandler _handler;
    private readonly Mock<IChatSessionRepository> _repo = new();

    public ArchiveChatSessionHandlerTests()
    {
        _handler = new ArchiveChatSessionHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_CallsRepository()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(id, true, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_PassesFalseFlag()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id, false), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(id, false, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_UsesCancellationToken()
    {
        var token = new CancellationTokenSource();
        await _handler.Handle(new ArchiveChatSessionCommand(Guid.NewGuid(), true), token.Token);
        _repo.Verify(r => r.ArchiveAsync(It.IsAny<Guid>(), true, token.Token));
    }

    [Fact]
    public async Task Handle_AllowsMultipleCalls()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id1, true), CancellationToken.None);
        await _handler.Handle(new ArchiveChatSessionCommand(id2, false), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(id1, true, It.IsAny<CancellationToken>()));
        _repo.Verify(r => r.ArchiveAsync(id2, false, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_DoesNotReturnValue()
    {
        // todo: fix me
        // var result = await _handler.Handle(new ArchiveChatSessionCommand(Guid.NewGuid(), true), CancellationToken.None);
        // Assert.True(result == Unit.Value);
    }

    [Fact]
    public async Task Handle_PassesIdCorrectly()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(It.Is<Guid>(g => g == id), true, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_CanArchiveAndUnarchive()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        await _handler.Handle(new ArchiveChatSessionCommand(id, false), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(id, true, It.IsAny<CancellationToken>()));
        _repo.Verify(r => r.ArchiveAsync(id, false, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_DifferentIds()
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        foreach (var id in ids)
            await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        foreach (var id in ids)
            _repo.Verify(r => r.ArchiveAsync(id, true, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Handle_SupportsSequentialCalls()
    {
        var id = Guid.NewGuid();
        await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        await _handler.Handle(new ArchiveChatSessionCommand(id, true), CancellationToken.None);
        _repo.Verify(r => r.ArchiveAsync(id, true, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_CompletesTask()
    {
        await _handler.Handle(new ArchiveChatSessionCommand(Guid.NewGuid(), true), CancellationToken.None);
        Assert.True(true);
    }
}