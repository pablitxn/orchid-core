namespace Domain.Exceptions;

public class DocumentNotFoundException : EntityNotFoundException
{
    public DocumentNotFoundException(Guid documentId)
        : base("Document", documentId)
    {
        DocumentId = documentId;
    }

    public Guid DocumentId { get; }
}
