using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Workflow.ListWorkflows;

public sealed class GetPublicWorkflowDetailsHandler(
    IWorkflowRepository workflowRepository
) : IRequestHandler<GetPublicWorkflowDetailsQuery, PublicWorkflowDetails?>
{
    public async Task<PublicWorkflowDetails?> Handle(GetPublicWorkflowDetailsQuery query, CancellationToken cancellationToken)
    {
        var workflow = await workflowRepository.GetByIdAsync(query.WorkflowId, cancellationToken);
        
        if (workflow == null)
        {
            return null;
        }

        return new PublicWorkflowDetails(
            Id: workflow.Id,
            Name: workflow.Name,
            Description: workflow.Description,
            PriceCredits: workflow.PriceCredits,
            Author: workflow.Author,
            Category: workflow.Category,
            Steps: workflow.Steps,
            EstimatedTime: workflow.EstimatedTime,
            Rating: workflow.Rating,
            Runs: workflow.Runs,
            Icon: workflow.Icon,
            Tags: workflow.Tags,
            DetailedDescription: workflow.DetailedDescription,
            Prerequisites: workflow.Prerequisites,
            InputRequirements: workflow.InputRequirements,
            OutputFormat: workflow.OutputFormat,
            CreatedAt: workflow.CreatedAt,
            UpdatedAt: workflow.UpdatedAt
        );
    }
}