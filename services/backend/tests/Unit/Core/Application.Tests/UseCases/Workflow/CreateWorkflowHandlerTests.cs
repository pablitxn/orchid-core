using Application.Interfaces;
using Application.UseCases.Workflow.CreateWorkflow;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Workflow;

public class CreateWorkflowHandlerTests
{
    private readonly Mock<IWorkflowRepository> _repositoryMock;
    private readonly Mock<ILogger<CreateWorkflowHandler>> _loggerMock;
    private readonly CreateWorkflowHandler _handler;

    public CreateWorkflowHandlerTests()
    {
        _repositoryMock = new Mock<IWorkflowRepository>();
        _loggerMock = new Mock<ILogger<CreateWorkflowHandler>>();
        _handler = new CreateWorkflowHandler(_repositoryMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_CreatesWorkflow_WithValidInput()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "New Workflow",
            Description: "A test workflow",
            PriceCredits: 25
        );

        WorkflowEntity? capturedWorkflow = null;
        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<WorkflowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowEntity, CancellationToken>((w, _) => capturedWorkflow = w)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("New Workflow", result.Name);
        Assert.Equal("A test workflow", result.Description);
        Assert.Equal(25, result.PriceCredits);
        Assert.NotNull(capturedWorkflow);
        Assert.NotEqual(Guid.Empty, capturedWorkflow.Id);
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenNameIsEmpty()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "",
            Description: "Description",
            PriceCredits: 10
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));

        _repositoryMock.Verify(
            x => x.CreateAsync(It.IsAny<WorkflowEntity>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenNameIsWhitespace()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "   ",
            Description: "Description",
            PriceCredits: 10
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ThrowsValidationException_WhenPriceIsNegative()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "Valid Name",
            Description: "Description",
            PriceCredits: -5
        );

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AllowsZeroPriceCredits()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "Free Workflow",
            Description: "A free workflow",
            PriceCredits: 0
        );

        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<WorkflowEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.PriceCredits);
    }

    [Fact]
    public async Task Handle_ThrowsOperationFailedException_WhenRepositoryFails()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "Valid Name",
            Description: "Description",
            PriceCredits: 10
        );

        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<WorkflowEntity>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<OperationFailedException>(() =>
            _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var command = new CreateWorkflowCommand(
            Name: "Timestamped Workflow",
            Description: "Test",
            PriceCredits: 15
        );

        WorkflowEntity? capturedWorkflow = null;
        var beforeCreate = DateTime.UtcNow;

        _repositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<WorkflowEntity>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowEntity, CancellationToken>((w, _) => capturedWorkflow = w)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.NotNull(capturedWorkflow);
        Assert.True(capturedWorkflow.CreatedAt >= beforeCreate);
        Assert.True(capturedWorkflow.CreatedAt <= afterCreate);
        Assert.True(capturedWorkflow.UpdatedAt >= beforeCreate);
        Assert.True(capturedWorkflow.UpdatedAt <= afterCreate);
    }
}
