namespace Domain.Exceptions;

public class WorkspaceAccessDeniedException : UnauthorizedDomainAccessException
{
    public WorkspaceAccessDeniedException(Guid workspaceId, Guid userId, string? reason = null)
        : base($"User '{userId}' does not have access to workspace '{workspaceId}'.{(reason != null ? $" Reason: {reason}" : "")}")
    {
        WorkspaceId = workspaceId;
        RequestingUserId = userId;
        Reason = reason;
    }

    public Guid WorkspaceId { get; }
    public Guid RequestingUserId { get; }
    public string? Reason { get; }
}
