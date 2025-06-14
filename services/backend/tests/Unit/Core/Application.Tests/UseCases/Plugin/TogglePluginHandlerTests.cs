using Application.Interfaces;
using Application.UseCases.Plugin.TogglePlugin;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Plugin;

public class TogglePluginHandlerTests
{
    private readonly TogglePluginHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();

    public TogglePluginHandlerTests()
    {
        _handler = new TogglePluginHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_UpdatesPlugin()
    {
        var plugin = new PluginEntity { Id = Guid.NewGuid(), Name = "p", IsActive = false };
        _repo.Setup(r => r.GetByIdAsync(plugin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(plugin);

        var result = await _handler.Handle(new TogglePluginCommand(plugin.Id, true), CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(plugin, It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(result);
        Assert.True(plugin.IsActive);
    }
}