namespace Domain.Exceptions;

public class ActiveSubscriptionRequiredException : Exception
{
    public ActiveSubscriptionRequiredException(string action)
        : base($"An active subscription is required to {action}.")
    {
        Action = action;
    }

    public string Action { get; }
}
