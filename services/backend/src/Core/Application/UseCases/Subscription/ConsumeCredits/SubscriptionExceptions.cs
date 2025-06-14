namespace Application.UseCases.Subscription.ConsumeCredits;

/// <summary>
///     Exception thrown when a subscription is not found for a given user.
/// </summary>
public class SubscriptionNotFoundException : Exception
{
    public SubscriptionNotFoundException(Guid userId)
        : base($"Subscription not found for user '{userId}'.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}

/// <summary>
///     Exception thrown when attempting to consume more credits than available.
/// </summary>
public class InsufficientCreditsException : Exception
{
    public InsufficientCreditsException(int available, int requested)
        : base($"Insufficient credits: available {available}, requested {requested}.")
    {
        Available = available;
        Requested = requested;
    }

    public int Available { get; }
    public int Requested { get; }
}