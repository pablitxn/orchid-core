using Application.UseCases.Workflow.Common;
using MediatR;

namespace Application.UseCases.Workflow.CreateWorkflow;

public record CreateWorkflowCommand(string Name, string? Description, int PriceCredits) : IRequest<WorkflowDto>;
