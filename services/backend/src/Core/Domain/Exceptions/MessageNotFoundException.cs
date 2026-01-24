namespace Domain.Exceptions;

public class MessageNotFoundException : EntityNotFoundException
{
    public MessageNotFoundException(Guid messageId)
        : base("Message", messageId)
    {
        MessageId = messageId;
    }

    public Guid MessageId { get; }
}
