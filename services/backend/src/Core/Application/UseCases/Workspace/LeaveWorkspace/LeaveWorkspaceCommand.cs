using MediatR;

namespace Application.UseCases.Workspace.LeaveWorkspace;

public sealed record LeaveWorkspaceCommand(
    Guid WorkspaceId,
    Guid UserId
) : IRequest<Unit>;
