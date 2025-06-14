using Domain.Entities;
using MediatR;

namespace Application.UseCases.Workflow.GetWorkflowById;

public record GetWorkflowByIdQuery(Guid Id) : IRequest<WorkflowEntity?>;

