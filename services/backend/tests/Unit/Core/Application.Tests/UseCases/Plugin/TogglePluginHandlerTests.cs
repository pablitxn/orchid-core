using Application.Interfaces;
using Application.UseCases.Plugin.TogglePlugin;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
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
        Assert.Equal(Unit.Value, result);
        Assert.True(plugin.IsActive);
    }

    [Fact]
    public async Task Handle_ThrowsPluginNotFoundException_WhenPluginDoesNotExist()
    {
        var pluginId = Guid.NewGuid();
        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>())).ReturnsAsync((PluginEntity?)null);

        await Assert.ThrowsAsync<PluginNotFoundException>(
            () => _handler.Handle(new TogglePluginCommand(pluginId, true), CancellationToken.None));

        _repo.Verify(r => r.UpdateAsync(It.IsAny<PluginEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}