namespace Domain.Exceptions;

public class SubscriptionNotFoundException : EntityNotFoundException
{
    public SubscriptionNotFoundException(Guid userId)
        : base($"Subscription not found for user '{userId}'.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}
