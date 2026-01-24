using Application.UseCases.Workflow.Common;
using MediatR;

namespace Application.UseCases.Workflow.GetWorkflowById;

public record GetWorkflowByIdQuery(Guid Id) : IRequest<WorkflowDto?>;

