using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.ListOpenApiPlugins;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.OpenApiPlugin;

public class ListOpenApiPluginsHandlerTests
{
    private readonly ListOpenApiPluginsHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();

    public ListOpenApiPluginsHandlerTests()
    {
        _handler = new ListOpenApiPluginsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyOpenApiPlugins()
    {
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "OpenAPI Plugin 1", PluginType = "openapi", SystemName = "openapi_1" },
            new() { Id = Guid.NewGuid(), Name = "Builtin Plugin", PluginType = "builtin", SystemName = "builtin_1" },
            new() { Id = Guid.NewGuid(), Name = "OpenAPI Plugin 2", PluginType = "openapi", SystemName = "openapi_2" }
        };

        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var result = await _handler.Handle(new ListOpenApiPluginsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Contains("OpenAPI Plugin", p.Name));
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoOpenApiPlugins()
    {
        var plugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "Builtin Plugin", PluginType = "builtin", SystemName = "builtin_1" }
        };

        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(plugins);

        var result = await _handler.Handle(new ListOpenApiPluginsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoPlugins()
    {
        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PluginEntity>());

        var result = await _handler.Handle(new ListOpenApiPluginsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }
}
