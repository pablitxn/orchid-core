using Application.Interfaces;
using Application.UseCases.Workflow.ListWorkflows;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Workflow;

public class ListPublicWorkflowsHandlerTests
{
    private readonly Mock<IWorkflowRepository> _workflowRepositoryMock;
    private readonly Mock<ILogger<ListPublicWorkflowsHandler>> _loggerMock;
    private readonly ListPublicWorkflowsHandler _handler;

    public ListPublicWorkflowsHandlerTests()
    {
        _workflowRepositoryMock = new Mock<IWorkflowRepository>();
        _loggerMock = new Mock<ILogger<ListPublicWorkflowsHandler>>();
        _handler = new ListPublicWorkflowsHandler(
            _workflowRepositoryMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPublicActiveWorkflows()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery();

        var workflows = new List<WorkflowEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Public Workflow",
                Description = "A public workflow",
                PriceCredits = 30,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow.AddDays(-5),
                Rating = 4.5,
                Runs = 100,
                CreatedAt = DateTime.UtcNow.AddDays(-10)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Private Workflow",
                Description = "A private workflow",
                PriceCredits = 20,
                IsPublic = false,
                IsActive = true,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Inactive Workflow",
                Description = "An inactive workflow",
                PriceCredits = 10,
                IsPublic = true,
                IsActive = false,
                PublishedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            }
        };

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Workflows.Items); // Only the public, active, published one
        Assert.Equal("Public Workflow", result.Workflows.Items[0].Name);
    }

    [Fact]
    public async Task Handle_FiltersWorkflows_BySearchTerm()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery { SearchTerm = "automation" };

        var workflows = new List<WorkflowEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Automation Workflow",
                Description = "Automates tasks",
                PriceCredits = 30,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow,
                Tags = "automation,tasks"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Manual Workflow",
                Description = "For manual processes",
                PriceCredits = 20,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow,
                Tags = "manual"
            }
        };

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Workflows.Items);
        Assert.Equal("Automation Workflow", result.Workflows.Items[0].Name);
    }

    [Fact]
    public async Task Handle_FiltersWorkflows_ByCategory()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery { Category = "Productivity" };

        var workflows = new List<WorkflowEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Productivity Workflow",
                Description = "Boosts productivity",
                PriceCredits = 25,
                Category = "Productivity",
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Entertainment Workflow",
                Description = "For fun",
                PriceCredits = 15,
                Category = "Entertainment",
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow
            }
        };

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Workflows.Items);
        Assert.Equal("Productivity Workflow", result.Workflows.Items[0].Name);
    }

    [Fact]
    public async Task Handle_FiltersWorkflows_ByPriceRange()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery { MinPrice = 20, MaxPrice = 40 };

        var workflows = new List<WorkflowEntity>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Cheap Workflow",
                PriceCredits = 10,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Mid-Range Workflow",
                PriceCredits = 30,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Expensive Workflow",
                PriceCredits = 50,
                IsPublic = true,
                IsActive = true,
                PublishedAt = DateTime.UtcNow
            }
        };

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Workflows.Items);
        Assert.Equal("Mid-Range Workflow", result.Workflows.Items[0].Name);
    }

    [Fact]
    public async Task Handle_PaginatesResults_Correctly()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery { Page = 2, PageSize = 2 };

        var workflows = Enumerable.Range(1, 5).Select(i => new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Name = $"Workflow {i}",
            PriceCredits = i * 10,
            Rating = 5 - (i * 0.1),
            IsPublic = true,
            IsActive = true,
            PublishedAt = DateTime.UtcNow
        }).ToList();

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workflows);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(5, result.Workflows.TotalCount);
        Assert.Equal(2, result.Workflows.Page);
        Assert.Equal(2, result.Workflows.Items.Count);
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenNoWorkflowsMatch()
    {
        // Arrange
        var query = new ListPublicWorkflowsQuery();

        _workflowRepositoryMock
            .Setup(x => x.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<WorkflowEntity>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Workflows.Items);
        Assert.Equal(0, result.Workflows.TotalCount);
    }
}
