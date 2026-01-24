namespace Domain.Exceptions;

public class ChatSessionNotFoundException : EntityNotFoundException
{
    public ChatSessionNotFoundException(Guid sessionId)
        : base("ChatSession", sessionId)
    {
        SessionId = sessionId;
    }

    public ChatSessionNotFoundException(string sessionId)
        : base($"ChatSession with SessionId '{sessionId}' was not found.")
    {
        SessionIdString = sessionId;
    }

    public Guid? SessionId { get; }
    public string? SessionIdString { get; }
}
