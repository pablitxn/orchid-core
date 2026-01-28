using Application.Interfaces;
using Application.UseCases.Workflow.GetWorkflowById;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Workflow;

public class GetWorkflowByIdHandlerTests
{
    private readonly Mock<IWorkflowRepository> _repositoryMock;
    private readonly GetWorkflowByIdHandler _handler;

    public GetWorkflowByIdHandlerTests()
    {
        _repositoryMock = new Mock<IWorkflowRepository>();
        _handler = new GetWorkflowByIdHandler(_repositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsWorkflowDto_WhenWorkflowExists()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetWorkflowByIdQuery(workflowId);

        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            Description = "A test workflow description",
            PriceCredits = 30,
            Author = "Test Author",
            Category = "Testing",
            Steps = 3,
            EstimatedTime = "5 minutes",
            Rating = 4.0,
            Runs = 50,
            IsPublic = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow
        };

        _repositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.Id);
        Assert.Equal("Test Workflow", result.Name);
        Assert.Equal(30, result.PriceCredits);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenWorkflowNotFound()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetWorkflowByIdQuery(workflowId);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsRepository_WithCorrectId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetWorkflowByIdQuery(workflowId);

        _repositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _repositoryMock.Verify(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
