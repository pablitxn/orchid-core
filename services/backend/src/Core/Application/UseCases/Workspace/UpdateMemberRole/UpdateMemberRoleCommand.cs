using Application.UseCases.Workspace.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Workspace.UpdateMemberRole;

public sealed record UpdateMemberRoleCommand(
    Guid WorkspaceId,
    Guid TargetUserId,
    WorkspaceRole NewRole,
    Guid RequestingUserId
) : IRequest<WorkspaceMemberDto>;
