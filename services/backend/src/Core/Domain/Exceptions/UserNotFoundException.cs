namespace Domain.Exceptions;

public class UserNotFoundException : EntityNotFoundException
{
    public UserNotFoundException(Guid userId)
        : base("User", userId)
    {
        UserId = userId;
    }

    public UserNotFoundException(string message)
        : base(message)
    {
    }

    public Guid UserId { get; }
}
