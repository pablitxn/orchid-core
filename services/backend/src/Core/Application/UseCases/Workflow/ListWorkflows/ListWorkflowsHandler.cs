using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public class ListWorkflowsHandler(IWorkflowRepository repository) : IRequestHandler<ListWorkflowsQuery, List<WorkflowEntity>>
{
    private readonly IWorkflowRepository _repository = repository;

    public async Task<List<WorkflowEntity>> Handle(ListWorkflowsQuery request, CancellationToken cancellationToken)
    {
        return await _repository.ListAsync(cancellationToken);
    }
}
