using Application.Interfaces;
using Application.UseCases.Plugin.CreatePlugin;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Plugin;

public class CreatePluginHandlerTests
{
    private readonly CreatePluginHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();

    public CreatePluginHandlerTests()
    {
        _handler = new CreatePluginHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_CreatesPlugin()
    {
        var cmd = new CreatePluginCommand("Test", "desc", "http://example.com");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.CreateAsync(It.IsAny<PluginEntity>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(cmd.Name, result.Name);
        Assert.False(result.IsActive);
    }
}