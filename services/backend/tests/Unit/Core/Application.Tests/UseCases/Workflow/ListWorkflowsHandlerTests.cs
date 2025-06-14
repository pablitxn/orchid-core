using Application.Interfaces;
using Application.UseCases.Workflow.ListWorkflows;
using Domain.Entities;
using Moq;

namespace Application.Tests.UseCases.Workflow;

public class ListWorkflowsHandlerTests
{
    private readonly ListWorkflowsHandler _handler;
    private readonly Mock<IWorkflowRepository> _repo = new();

    public ListWorkflowsHandlerTests()
    {
        _handler = new ListWorkflowsHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsWorkflows()
    {
        var expected = new List<WorkflowEntity> { new() { Id = Guid.NewGuid(), Name = "w" } };
        _repo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await _handler.Handle(new ListWorkflowsQuery(), CancellationToken.None);

        Assert.Equal(expected.Count, result.Count);
    }
}
