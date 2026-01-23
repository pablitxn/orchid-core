namespace Domain.Exceptions;

public class SubscriptionPlanNotFoundException : EntityNotFoundException
{
    public SubscriptionPlanNotFoundException(string planName)
        : base($"Subscription plan '{planName}' not found in database.")
    {
        PlanName = planName;
    }

    public string PlanName { get; }
}
