using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.UpdateOpenApiPlugin;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace Application.Tests.UseCases.OpenApiPlugin;

public class UpdateOpenApiPluginHandlerTests
{
    private readonly UpdateOpenApiPluginHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();
    private readonly Mock<ILogger<UpdateOpenApiPluginHandler>> _logger = new();

    public UpdateOpenApiPluginHandlerTests()
    {
        _handler = new UpdateOpenApiPluginHandler(_repo.Object, _logger.Object);
    }

    [Fact]
    public async Task Handle_UpdatesOpenApiPlugin()
    {
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Original Name",
            PluginType = "openapi",
            SystemName = "test_plugin",
            BaseUrl = "https://old.example.com"
        };

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        var cmd = new UpdateOpenApiPluginCommand(pluginId, Name: "New Name");

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("New Name", result.Name);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<PluginEntity>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InvalidatesCache_WhenBaseUrlChanges()
    {
        var pluginId = Guid.NewGuid();
        var plugin = new PluginEntity
        {
            Id = pluginId,
            Name = "Test Plugin",
            PluginType = "openapi",
            SystemName = "test_plugin",
            BaseUrl = "https://old.example.com",
            OpenApiSpecCache = "cached spec",
            SpecCachedAt = DateTime.UtcNow
        };

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugin);

        var cmd = new UpdateOpenApiPluginCommand(pluginId, BaseUrl: "https://new.example.com");

        await _handler.Handle(cmd, CancellationToken.None);

        _repo.Verify(r => r.UpdateAsync(
            It.Is<PluginEntity>(p =>
                p.OpenApiSpecCache == null &&
                p.SpecCachedAt == null &&
                p.BaseUrl == "https://new.example.com"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsPluginNotFoundException_WhenPluginNotFound()
    {
        var pluginId = Guid.NewGuid();

        _repo.Setup(r => r.GetByIdAsync(pluginId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PluginEntity?)null);

        await Assert.ThrowsAsync<PluginNotFoundException>(
            () => _handler.Handle(new UpdateOpenApiPluginCommand(pluginId), CancellationToken.None));
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
            () => _handler.Handle(new UpdateOpenApiPluginCommand(pluginId, Name: "New Name"), CancellationToken.None));
    }
}
