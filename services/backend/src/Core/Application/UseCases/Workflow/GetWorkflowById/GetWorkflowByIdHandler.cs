using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Workflow.GetWorkflowById;

public class GetWorkflowByIdHandler(IWorkflowRepository repository)
    : IRequestHandler<GetWorkflowByIdQuery, WorkflowEntity?>
{
    public async Task<WorkflowEntity?> Handle(GetWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        return await repository.GetByIdAsync(request.Id, cancellationToken);
    }
}