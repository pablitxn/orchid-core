namespace Domain.Exceptions;

public class PublisherNotFoundException : EntityNotFoundException
{
    public PublisherNotFoundException(Guid publisherId)
        : base("Publisher", publisherId.ToString()) { }

    public PublisherNotFoundException(string message)
        : base(message) { }
}
