namespace Application.UseCases.Workflow.PurchaseWorkflow
{
    public enum PurchaseResult
    {
        Success,
        WorkflowNotFound,
        UserNotFound,
        InsufficientCredits,
        AlreadyPurchased
    }
}

