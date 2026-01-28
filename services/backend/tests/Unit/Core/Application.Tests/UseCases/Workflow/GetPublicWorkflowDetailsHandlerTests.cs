using Application.Interfaces;
using Application.UseCases.Workflow.ListWorkflows;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Workflow;

public class GetPublicWorkflowDetailsHandlerTests
{
    private readonly Mock<IWorkflowRepository> _workflowRepositoryMock;
    private readonly GetPublicWorkflowDetailsHandler _handler;

    public GetPublicWorkflowDetailsHandlerTests()
    {
        _workflowRepositoryMock = new Mock<IWorkflowRepository>();
        _handler = new GetPublicWorkflowDetailsHandler(_workflowRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsWorkflowDetails_WhenWorkflowExists()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetPublicWorkflowDetailsQuery(workflowId);

        var workflow = new WorkflowEntity
        {
            Id = workflowId,
            Name = "Test Workflow",
            Description = "A test workflow",
            PriceCredits = 50,
            Author = "John Doe",
            Category = "Productivity",
            Steps = 5,
            EstimatedTime = "10 minutes",
            Rating = 4.5,
            Runs = 100,
            Icon = "🚀",
            Tags = "automation,productivity",
            DetailedDescription = "Detailed description here",
            Prerequisites = "None",
            InputRequirements = "Text input",
            OutputFormat = "JSON",
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflow);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(workflowId, result.Id);
        Assert.Equal("Test Workflow", result.Name);
        Assert.Equal(50, result.PriceCredits);
        Assert.Equal("John Doe", result.Author);
        Assert.Equal("Productivity", result.Category);
        Assert.Equal(5, result.Steps);
        Assert.Equal(4.5, result.Rating);
        Assert.Equal(100, result.Runs);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenWorkflowNotFound()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetPublicWorkflowDetailsQuery(workflowId);

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsRepository_WithCorrectWorkflowId()
    {
        // Arrange
        var workflowId = Guid.NewGuid();
        var query = new GetPublicWorkflowDetailsQuery(workflowId);

        _workflowRepositoryMock
            .Setup(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkflowEntity?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _workflowRepositoryMock.Verify(x => x.GetByIdAsync(workflowId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
