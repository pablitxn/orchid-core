namespace Domain.Exceptions;

public class PublisherAlreadyExistsException : Exception
{
    public Guid UserId { get; }

    public PublisherAlreadyExistsException(Guid userId)
        : base($"A publisher already exists for user '{userId}'.")
    {
        UserId = userId;
    }
}
