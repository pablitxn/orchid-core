namespace Domain.Exceptions;

public class WorkspaceNotFoundException : EntityNotFoundException
{
    public WorkspaceNotFoundException(Guid id)
        : base("Workspace", id) { }
}
