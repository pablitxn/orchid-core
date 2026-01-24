using Application.Interfaces;
using Application.UseCases.Workflow.Common;
using MediatR;

namespace Application.UseCases.Workflow.GetWorkflowById;

public class GetWorkflowByIdHandler(IWorkflowRepository repository)
    : IRequestHandler<GetWorkflowByIdQuery, WorkflowDto?>
{
    public async Task<WorkflowDto?> Handle(GetWorkflowByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByIdAsync(request.Id, cancellationToken);
        return entity is null ? null : WorkflowDto.FromEntity(entity);
    }
}