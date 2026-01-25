namespace Domain.Exceptions;

public class SubscriptionPlanNotFoundException : EntityNotFoundException
{
    public SubscriptionPlanNotFoundException(string planName)
        : base($"Subscription plan '{planName}' not found in database.")
    {
        PlanName = planName;
    }

    public SubscriptionPlanNotFoundException(Guid planId)
        : base($"Subscription plan with ID '{planId}' not found.")
    {
        PlanId = planId;
    }

    public string? PlanName { get; }
    public Guid? PlanId { get; }
}
