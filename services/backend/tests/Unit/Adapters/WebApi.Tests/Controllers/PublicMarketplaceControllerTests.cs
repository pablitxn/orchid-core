using Application.UseCases.Plugin.ListAvailablePlugins;
using Application.UseCases.Workflow.ListWorkflows;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class PublicMarketplaceControllerTests
{
    private readonly PublicMarketplaceController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public PublicMarketplaceControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new PublicMarketplaceController(_mediatorMock.Object);
    }

    #region ListPlugins Tests

    [Fact]
    public async Task ListPlugins_ReturnsOk_WithPluginList()
    {
        // Arrange
        var expectedPlugins = new List<PublicPluginDto>
        {
            new(Guid.NewGuid(), "Plugin 1", "Desc 1", 100, false, true, null, DateTime.UtcNow),
            new(Guid.NewGuid(), "Plugin 2", "Desc 2", 200, true, true, null, DateTime.UtcNow)
        };

        var result = new ListPublicPluginsDto(expectedPlugins);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ListPlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(result, okResult.Value);
    }

    [Fact]
    public async Task ListPlugins_ReturnsOk_WithEmptyList_WhenNoPluginsAvailable()
    {
        // Arrange
        var emptyList = new ListPublicPluginsDto(new List<PublicPluginDto>());

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        // Act
        var actionResult = await _controller.ListPlugins(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<ListPublicPluginsDto>(okResult.Value);
        Assert.Empty(result.Plugins);
    }

    #endregion

    #region ListWorkflows Tests

    [Fact]
    public async Task ListWorkflows_ReturnsOk_WithWorkflowList()
    {
        // Arrange
        var expectedWorkflows = new List<PublicWorkflowDto>
        {
            new(Guid.NewGuid(), "Workflow 1", "Desc 1", 100, "Author1", "Cat1", 5, "10m", 4.5, 100, null, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow),
            new(Guid.NewGuid(), "Workflow 2", "Desc 2", 200, "Author2", "Cat2", 3, "5m", 4.8, 50, null, null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow)
        };

        var paginatedResult = new Application.Common.Models.PaginatedResult<PublicWorkflowDto>(
            expectedWorkflows, expectedWorkflows.Count, 1, 20);
        var result = new ListPublicWorkflowsDto(paginatedResult);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicWorkflowsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ListWorkflows(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(result, okResult.Value);
    }

    [Fact]
    public async Task ListWorkflows_ReturnsOk_WithEmptyList_WhenNoWorkflowsAvailable()
    {
        // Arrange
        var emptyPaginated = Application.Common.Models.PaginatedResult<PublicWorkflowDto>.Empty();
        var emptyList = new ListPublicWorkflowsDto(emptyPaginated);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicWorkflowsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        // Act
        var actionResult = await _controller.ListWorkflows(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var result = Assert.IsType<ListPublicWorkflowsDto>(okResult.Value);
        Assert.Empty(result.Workflows.Items);
    }

    #endregion

    #region GetPluginDetails Tests

    [Fact]
    public async Task GetPluginDetails_ReturnsOk_WhenPluginExists()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var expectedPlugin = new PublicPluginDetailsDto(
            pluginId, "Test Plugin", "Test Description", 100, false, true, "http://example.com", "test-plugin", DateTime.UtcNow, DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
            It.Is<GetPublicPluginDetailsQuery>(q => q.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugin);

        // Act
        var result = await _controller.GetPluginDetails(pluginId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedPlugin, okResult.Value);
    }

    [Fact]
    public async Task GetPluginDetails_ReturnsNotFound_WhenPluginDoesNotExist()
    {
        // Arrange
        var pluginId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<GetPublicPluginDetailsQuery>(q => q.PluginId == pluginId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicPluginDetailsDto?)null);

        // Act
        var result = await _controller.GetPluginDetails(pluginId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetPluginDetails_CallsMediatorWithCorrectQuery()
    {
        // Arrange
        var pluginId = Guid.NewGuid();
        var expectedPlugin = new PublicPluginDetailsDto(
            pluginId, "Test Plugin", null, 100, false, true, null, null, DateTime.UtcNow, DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<GetPublicPluginDetailsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlugin);

        // Act
        await _controller.GetPluginDetails(pluginId, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetPublicPluginDetailsQuery>(q => q.PluginId == pluginId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetWorkflowDetails Tests

    [Fact]
    public async Task GetWorkflowDetails_ReturnsOk_WhenWorkflowExists()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var expectedWorkflow = new PublicWorkflowDetailsDto(
            workflowId, "Test Workflow", "Test Description", 200, "Author", "Category", 5, "10m", 
            4.5, 100, null, null, null, null, null, null, DateTime.UtcNow, DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
            It.Is<GetPublicWorkflowDetailsQuery>(q => q.WorkflowId == workflowId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWorkflow);

        // Act
        var result = await _controller.GetWorkflowDetails(workflowId, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(expectedWorkflow, okResult.Value);
    }

    [Fact]
    public async Task GetWorkflowDetails_ReturnsNotFound_WhenWorkflowDoesNotExist()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        _mediatorMock.Setup(m => m.Send(
            It.Is<GetPublicWorkflowDetailsQuery>(q => q.WorkflowId == workflowId),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((PublicWorkflowDetailsDto?)null);

        // Act
        var result = await _controller.GetWorkflowDetails(workflowId, CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetWorkflowDetails_CallsMediatorWithCorrectQuery()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var expectedWorkflow = new PublicWorkflowDetailsDto(
            workflowId, "Test Workflow", null, 200, null, null, 0, null, 0, 0, null, null, null, null, null, null, DateTime.UtcNow, DateTime.UtcNow);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<GetPublicWorkflowDetailsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWorkflow);

        // Act
        await _controller.GetWorkflowDetails(workflowId, CancellationToken.None);

        // Assert
        _mediatorMock.Verify(m => m.Send(
            It.Is<GetPublicWorkflowDetailsQuery>(q => q.WorkflowId == workflowId),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion
}
