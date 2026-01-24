namespace Application.UseCases.Workflow.PurchaseWorkflow
{
    public enum PurchaseOutcome
    {
        Success,
        WorkflowNotFound,
        UserNotFound,
        InsufficientCredits,
        AlreadyPurchased
    }
}

