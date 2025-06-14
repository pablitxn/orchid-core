using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed record GetPublicWorkflowDetailsQuery(Guid WorkflowId) : IRequest<PublicWorkflowDetails?>;