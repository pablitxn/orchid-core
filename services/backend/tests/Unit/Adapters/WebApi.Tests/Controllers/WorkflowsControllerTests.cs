using System.Security.Claims;
using Application.UseCases.Workflow.Common;
using Application.UseCases.Workflow.CreateWorkflow;
using Application.UseCases.Workflow.GetWorkflowById;
using Application.UseCases.Workflow.ListWorkflows;
using Application.UseCases.Workflow.PurchaseWorkflow;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class WorkflowsControllerTests
{
    private readonly WorkflowsController _controller;
    private readonly Mock<IMediator> _mediatorMock;

    public WorkflowsControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new WorkflowsController(_mediatorMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private void SetupUnauthenticatedUser()
    {
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
        };
    }

    #endregion

    #region GetWorkflows Tests

    [Fact]
    public async Task GetWorkflows_ReturnsOk_WithWorkflowsList()
    {
        // Arrange
        var workflows = new List<WorkflowDto>
        {
            new() { Id = Guid.NewGuid(), Name = "Workflow 1" },
            new() { Id = Guid.NewGuid(), Name = "Workflow 2" }
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _controller.GetWorkflows();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(workflows, okResult.Value);
    }

    [Fact]
    public async Task GetWorkflows_ReturnsOk_WithEmptyList_WhenNoWorkflows()
    {
        // Arrange
        var workflows = new List<WorkflowDto>();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _controller.GetWorkflows();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(workflows, okResult.Value);
    }

    [Fact]
    public async Task GetWorkflows_CallsMediator_WithListWorkflowsQuery()
    {
        // Arrange
        _mediatorMock
            .Setup(m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowDto>());

        // Act
        await _controller.GetWorkflows();

        // Assert
        _mediatorMock.Verify(
            m => m.Send(It.IsAny<ListWorkflowsQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetById_ReturnsOk_WithWorkflow_WhenWorkflowExists()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow"
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetWorkflowByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        // Act
        var result = await _controller.GetById(workflowId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(workflow, okResult.Value);
    }

    [Fact]
    public async Task GetById_ReturnsNotFound_WhenWorkflowDoesNotExist()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetWorkflowByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        var result = await _controller.GetById(workflowId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetById_CallsMediator_WithCorrectWorkflowId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<GetWorkflowByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        await _controller.GetById(workflowId);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<GetWorkflowByIdQuery>(q => q.Id == workflowId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task Create_ReturnsCreatedAtAction_WithWorkflow()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var command = new CreateWorkflowCommand("New Workflow", "Test Description", 100);

        var createdWorkflow = new WorkflowDto
        {
            Id = workflowId,
            Name = command.Name,
            Description = command.Description
        };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWorkflow);

        // Act
        var result = await _controller.Create(command);

        // Assert
        var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(nameof(WorkflowsController.GetById), createdAtActionResult.ActionName);
        var routeValues = Assert.IsType<RouteValueDictionary>(createdAtActionResult.RouteValues);
        Assert.Equal(workflowId, routeValues["id"]);
        Assert.Equal(createdWorkflow, createdAtActionResult.Value);
    }

    [Fact]
    public async Task Create_CallsMediator_WithCorrectCommand()
    {
        // Arrange
        var command = new CreateWorkflowCommand("Test Workflow", "Description", 50);

        var createdWorkflow = new WorkflowDto { Id = Guid.NewGuid() };

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<CreateWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdWorkflow);

        // Act
        await _controller.Create(command);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(command, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Purchase Tests

    [Fact]
    public async Task Purchase_ReturnsUnauthorized_WhenUserIdClaimMissing()
    {
        // Arrange
        SetupUnauthenticatedUser();
        var workflowId = Guid.NewGuid();

        // Act
        var result = await _controller.Purchase(workflowId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Purchase_ReturnsUnauthorized_WhenUserIdClaimInvalid()
    {
        // Arrange
        var claims = new List<Claim>
        {
            new("sub", "not-a-valid-guid")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };

        var workflowId = Guid.NewGuid();

        // Act
        var result = await _controller.Purchase(workflowId);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Purchase_ReturnsOk_WithSuccessMessage_WhenPurchaseSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<PurchaseWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.Success);

        // Act
        var result = await _controller.Purchase(workflowId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);

        var messageProperty = response!.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty!.GetValue(response) as string;
        Assert.Equal("Workflow purchased successfully", message);
    }

    [Fact]
    public async Task Purchase_CallsMediator_WithCorrectCommand()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var workflowId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);

        _mediatorMock
            .Setup(m => m.Send(It.IsAny<PurchaseWorkflowCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PurchaseOutcome.Success);

        // Act
        await _controller.Purchase(workflowId);

        // Assert
        _mediatorMock.Verify(
            m => m.Send(
                It.Is<PurchaseWorkflowCommand>(cmd =>
                    cmd.UserId == userId && cmd.WorkflowId == workflowId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Purchase_HasAuthorizeAttribute()
    {
        // Arrange
        var method = typeof(WorkflowsController).GetMethod(nameof(WorkflowsController.Purchase));

        // Assert
        Assert.NotNull(method);
        var authorizeAttribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        Assert.Single(authorizeAttribute);
    }

    #endregion
}
