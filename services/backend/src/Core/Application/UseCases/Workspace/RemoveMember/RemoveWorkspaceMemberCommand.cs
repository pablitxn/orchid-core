using MediatR;

namespace Application.UseCases.Workspace.RemoveMember;

public sealed record RemoveWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid TargetUserId,
    Guid RequestingUserId
) : IRequest<Unit>;
