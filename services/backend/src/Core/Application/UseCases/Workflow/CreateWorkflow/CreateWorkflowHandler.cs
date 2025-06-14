using Application.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace Application.UseCases.Workflow.CreateWorkflow;

public class CreateWorkflowHandler(IWorkflowRepository repository, ILogger<CreateWorkflowHandler> logger)
    : IRequestHandler<CreateWorkflowCommand, WorkflowEntity>
{
    private readonly IWorkflowRepository _repository = repository;
    private readonly ILogger<CreateWorkflowHandler> _logger = logger;

    public async Task<WorkflowEntity> Handle(CreateWorkflowCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("Workflow name is required.");
        if (request.PriceCredits < 0)
            throw new ValidationException("PriceCredits must be a non-negative value.");

        var workflow = new WorkflowEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            PriceCredits = request.PriceCredits,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await _repository.CreateAsync(workflow, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create workflow with name {Name}", request.Name);
            throw new ApplicationException("An error occurred while creating the workflow.", ex);
        }

        return workflow;
    }
}