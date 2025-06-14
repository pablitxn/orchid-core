using Domain.Entities;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public record ListWorkflowsQuery() : IRequest<List<WorkflowEntity>>;
