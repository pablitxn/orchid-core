namespace Domain.Exceptions;

public class WorkspaceMemberNotFoundException : EntityNotFoundException
{
    public WorkspaceMemberNotFoundException(Guid workspaceId, Guid userId)
        : base($"Member with user ID '{userId}' not found in workspace '{workspaceId}'.") { }
}
