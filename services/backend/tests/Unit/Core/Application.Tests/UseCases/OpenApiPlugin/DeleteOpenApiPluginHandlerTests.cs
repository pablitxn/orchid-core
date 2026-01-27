using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.DeleteOpenApiPlugin;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.OpenApiPlugin;

public class DeleteOpenApiPluginHandlerTests
{
    private readonly DeleteOpenApiPluginHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();
    private readonly Mock<ILogger<DeleteOpenApiPluginHandler>> _logger = new();

    public DeleteOpenApiPluginHandlerTests()
    {
        _handler = new DeleteOpenApiPluginHandler(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_DeletesOpenApiPlugin()
    {
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            PluginType = "openapi",
            SystemName = "test_plugin"
        };

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        await _handler.Handle(new DeleteOpenApiPluginCommand(pluginId), CancellationToken.None);

        _repo.Verify(r => r.DeleteAsync(pluginId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsPluginNotFoundException_WhenPluginNotFound()
    {
        var pluginId = Guid.NewGuid();

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        await Assert.ThrowsAsync<PluginNotFoundException>(
            () => _handler.Handle(new DeleteOpenApiPluginCommand(pluginId), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenPluginIsNotOpenApi()
    {
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Builtin Plugin",
            PluginType = "builtin",
            SystemName = "builtin_plugin"
        };

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(new DeleteOpenApiPluginCommand(pluginId), CancellationToken.None));
    }
}
