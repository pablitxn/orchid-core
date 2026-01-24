using Application.Interfaces;
using Application.UseCases.Workflow.Common;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public class ListWorkflowsHandler(IWorkflowRepository repository) : IRequestHandler<ListWorkflowsQuery, List<WorkflowDto>>
{
    private readonly IWorkflowRepository _repository = repository;

    public async Task<List<WorkflowDto>> Handle(ListWorkflowsQuery request, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(cancellationToken);
        return entities.Select(WorkflowDto.FromEntity).ToList();
    }
}
