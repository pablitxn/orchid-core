namespace Domain.Exceptions;

public class AlreadySubscribedException : Exception
{
    public AlreadySubscribedException(Guid userId)
        : base($"User '{userId}' already has an active subscription.")
    {
        UserId = userId;
    }

    public Guid UserId { get; }
}
