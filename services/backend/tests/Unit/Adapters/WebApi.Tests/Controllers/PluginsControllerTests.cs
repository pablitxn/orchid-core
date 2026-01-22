using Application.UseCases.Plugin.CreatePlugin;
using Application.UseCases.Plugin.ListPlugins;
using Application.UseCases.Plugin.TogglePlugin;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class PluginsControllerTests
{
    private readonly PluginsController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public PluginsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new PluginsController(_mediatorMock.Object);
    }

    #region GetPlugins Tests

    [Fact]
    public async Task GetPlugins_ReturnsOk_WithPluginList()
    {
        // Arrange
        var expectedPlugins = new List<PluginEntity>
        {
            new() { Id = Guid.NewGuid(), Name = "Plugin 1", IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Plugin 2", IsActive = false }
        };

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugins);

        // Act
        var result = await _controller.GetPlugins();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedPlugins, okResult.Value);
    }

    [Fact]
    public async Task GetPlugins_ReturnsOk_WithEmptyList_WhenNoPluginsAvailable()
    {
        // Arrange
        var emptyList = new List<PluginEntity>();

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        // Act
        var result = await _controller.GetPlugins();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IEnumerable<PluginEntity>>(okResult.Value);
        Assert.Empty(list);
    }

    [Fact]
    public async Task GetPlugins_CallsMediatorWithListPluginsQuery()
    {
        // Arrange
        var expectedPlugins = new List<PluginEntity>();

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugins);

        // Act
        await _controller.GetPlugins();

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.IsAny<ListPluginsQuery>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region InstallPlugin Tests

    [Fact]
    public async Task InstallPlugin_ReturnsCreatedAtAction_WithCreatedPlugin()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var command = new CreatePluginCommand("New Plugin", "Test Description", null);

        var createdPlugin = new PluginEntity
        {
            Id = pluginId,
            Name = "New Plugin",
            Description = "Test Description"
        };

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<CreatePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPlugin);

        // Act
        var result = await _controller.InstallPlugin(command);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(PluginsController.GetPlugins), createdAtActionResult.ActionName);
        Assert.Equal(createdPlugin, createdAtActionResult.Value);
        
        var routeValues = createdAtActionResult.RouteValues;
        Assert.NotNull(routeValues);
        Assert.True(routeValues.ContainsKey("id"));
        Assert.Equal(pluginId, routeValues["id"]);
    }

    [Fact]
    public async Task InstallPlugin_CallsMediatorWithCorrectCommand()
    {
        // Arrange
        var command = new CreatePluginCommand("New Plugin", "Test Description", null);

        var createdPlugin = new PluginEntity { Id = Guid.NewGuid(), Name = "New Plugin" };

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<CreatePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdPlugin);

        // Act
        await _controller.InstallPlugin(command);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<CreatePluginCommand>(c => 
                c.Name == command.Name && 
                c.Description == command.Description),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Activate Tests

    [Fact]
    public async Task Activate_ReturnsOk_WhenPluginIsActivated()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == true),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Activate(pluginId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Activate_ReturnsNotFound_WhenPluginDoesNotExist()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == true),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Activate(pluginId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Activate_CallsMediatorWithCorrectCommand()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<TogglePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _controller.Activate(pluginId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == true),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public async Task Deactivate_ReturnsOk_WhenPluginIsDeactivated()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == false),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Deactivate(pluginId);

        // Assert
        Assert.IsType<OkResult>(result);
    }

    [Fact]
    public async Task Deactivate_ReturnsNotFound_WhenPluginDoesNotExist()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == false),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Deactivate(pluginId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Deactivate_CallsMediatorWithCorrectCommand()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<TogglePluginCommand>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _controller.Deactivate(pluginId);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<TogglePluginCommand>(c => c.Id == pluginId && c.Activate == false),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
