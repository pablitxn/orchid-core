using Application.Interfaces;
using Application.UseCases.Plugin.ListPlugins;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Plugin;

public class ListPluginsHandlerTests
{
    private readonly ListPluginsHandler _handler;
    private readonly Mock<IPluginRepository> _repo = new();

    public ListPluginsHandlerTests()
    {
        _handler = new ListPluginsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPlugins()
    {
        var expected = new List<PluginEntity> { new() { Id = Guid.NewGuid(), Name = "p" } };
        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _handler.Handle(new ListPluginsQuery(), CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }
}