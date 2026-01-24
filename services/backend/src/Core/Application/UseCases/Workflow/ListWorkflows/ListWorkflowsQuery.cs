using Application.UseCases.Workflow.Common;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public record ListWorkflowsQuery() : IRequest<List<WorkflowDto>>;
