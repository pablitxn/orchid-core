namespace Domain.Exceptions;

public class WorkflowNotFoundException : EntityNotFoundException
{
    public WorkflowNotFoundException(Guid workflowId)
        : base("Workflow", workflowId)
    {
        WorkflowId = workflowId;
    }

    public Guid WorkflowId { get; }
}
