using Application.UseCases.Workspace.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Workspace.AddMember;

public sealed record AddWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid InvitedUserId,
    WorkspaceRole Role,
    Guid InvitingUserId
) : IRequest<WorkspaceMemberDto>;
