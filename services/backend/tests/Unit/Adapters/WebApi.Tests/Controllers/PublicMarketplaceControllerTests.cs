using Application.Common.Models;
using Application.UseCases.Plugin.GetPublicPluginDetails;
using Application.UseCases.Plugin.ListPublicPlugins;
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
            CreatePublicPluginDto(Guid.NewGuid(), "Plugin 1", "Desc 1", 100),
            CreatePublicPluginDto(Guid.NewGuid(), "Plugin 2", "Desc 2", 200)
        };

        var paginatedPlugins = new PaginatedResult<PublicPluginDto>(expectedPlugins, 2, 1, 20);
        var result = new ListPublicPluginsDto(paginatedPlugins);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var actionResult = await _controller.ListPlugins(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(result, okResult.Value);
    }

    [Fact]
    public async Task ListPlugins_ReturnsOk_WithEmptyList_WhenNoPluginsAvailable()
    {
        // Arrange
        var emptyPaginated = PaginatedResult<PublicPluginDto>.Empty();
        var emptyList = new ListPublicPluginsDto(emptyPaginated);

        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyList);

        // Act
        var actionResult = await _controller.ListPlugins(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var resultDto = Assert.IsType<ListPublicPluginsDto>(okResult.Value);
        Assert.Empty(resultDto.Plugins.Items);
    }

    [Fact]
    public async Task ListPlugins_PassesFilterParametersToQuery()
    {
        // Arrange
        var emptyPaginated = PaginatedResult<PublicPluginDto>.Empty();
        var emptyList = new ListPublicPluginsDto(emptyPaginated);

        ListPublicPluginsQuery? capturedQuery = null;
        _mediatorMock.Setup(m => m.Send(
            It.IsAny<ListPublicPluginsQuery>(),
            It.IsAny<CancellationToken>()))
            .Callback<IRequest<ListPublicPluginsDto>, CancellationToken>((q, _) => capturedQuery = (ListPublicPluginsQuery)q)
            .ReturnsAsync(emptyList);

        // Act
        await _controller.ListPlugins(
            search: "test",
            category: "AI",
            minRating: 4.0m,
            page: 2,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.NotNull(capturedQuery);
        Assert.Equal("test", capturedQuery.SearchTerm);
        Assert.Equal("AI", capturedQuery.Category);
        Assert.Equal(4.0m, capturedQuery.MinRating);
        Assert.Equal(2, capturedQuery.Page);
        Assert.Equal(10, capturedQuery.PageSize);
    }

    #endregion

    #region ListWorkflows Tests

    [Fact]
    public async Task ListWorkflows_ReturnsOk_WithWorkflowList()
    {
        // Arrange
        var expectedWorkflows = new List<PublicWorkflowDto>
        {
            CreatePublicWorkflowDto(Guid.NewGuid(), "Workflow 1", "Desc 1", 100),
            CreatePublicWorkflowDto(Guid.NewGuid(), "Workflow 2", "Desc 2", 200)
        };

        var paginatedResult = new PaginatedResult<PublicWorkflowDto>(
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
        var emptyPaginated = PaginatedResult<PublicWorkflowDto>.Empty();
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
        var expectedPlugin = CreatePublicPluginDetailsDto(pluginId, "Test Plugin", "Test Description", 100);

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
        var expectedPlugin = CreatePublicPluginDetailsDto(pluginId, "Test Plugin", null, 100);

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
        var expectedWorkflow = CreatePublicWorkflowDetailsDto(workflowId, "Test Workflow", "Test Description", 200);

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
        var expectedWorkflow = CreatePublicWorkflowDetailsDto(workflowId, "Test Workflow", null, 200);

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

    #region Helper Methods

    private static PublicPluginDto CreatePublicPluginDto(
        Guid id,
        string name,
        string? description,
        int priceCredits,
        bool isSubscriptionBased = false,
        bool isActive = true)
    {
        return new PublicPluginDto(
            Id: id,
            Name: name,
            Description: description,
            PriceCredits: priceCredits,
            IsSubscriptionBased: isSubscriptionBased,
            IsActive: isActive,
            SourceUrl: null,
            Author: "TestAuthor",
            Category: "TestCategory",
            Icon: null,
            Tags: null,
            Rating: 4.5,
            ReviewCount: 10,
            InstallCount: 100,
            IsFeatured: false,
            Version: "1.0.0",
            Compatibility: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            PublishedAt: DateTime.UtcNow);
    }

    private static PublicPluginDetailsDto CreatePublicPluginDetailsDto(
        Guid id,
        string name,
        string? description,
        int priceCredits)
    {
        return new PublicPluginDetailsDto(
            Id: id,
            Name: name,
            Description: description,
            PriceCredits: priceCredits,
            IsSubscriptionBased: false,
            IsActive: true,
            SourceUrl: null,
            SystemName: "test-plugin",
            Author: "TestAuthor",
            Category: "TestCategory",
            Icon: null,
            Tags: null,
            Rating: 4.5,
            ReviewCount: 10,
            InstallCount: 100,
            IsFeatured: false,
            Version: "1.0.0",
            Compatibility: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            PublishedAt: DateTime.UtcNow);
    }

    private static PublicWorkflowDto CreatePublicWorkflowDto(
        Guid id,
        string name,
        string? description,
        int priceCredits)
    {
        return new PublicWorkflowDto(
            Id: id,
            Name: name,
            Description: description,
            PriceCredits: priceCredits,
            Author: "TestAuthor",
            Category: "TestCategory",
            Steps: 5,
            EstimatedTime: "10m",
            Rating: 4.5,
            Runs: 100,
            Icon: null,
            Tags: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow,
            PublishedAt: DateTime.UtcNow);
    }

    private static PublicWorkflowDetailsDto CreatePublicWorkflowDetailsDto(
        Guid id,
        string name,
        string? description,
        int priceCredits)
    {
        return new PublicWorkflowDetailsDto(
            Id: id,
            Name: name,
            Description: description,
            PriceCredits: priceCredits,
            Author: "TestAuthor",
            Category: "TestCategory",
            Steps: 5,
            EstimatedTime: "10m",
            Rating: 4.5,
            Runs: 100,
            Icon: null,
            Tags: null,
            DetailedDescription: null,
            Prerequisites: null,
            InputRequirements: null,
            OutputFormat: null,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: DateTime.UtcNow);
    }

    #endregion
}
