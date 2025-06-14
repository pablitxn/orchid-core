using MediatR;

namespace Application.UseCases.Workflow.PurchaseWorkflow;

public record PurchaseWorkflowCommand(Guid UserId, Guid WorkflowId) : IRequest<PurchaseResult>;
