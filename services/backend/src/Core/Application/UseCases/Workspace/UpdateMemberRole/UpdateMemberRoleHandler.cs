using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.UpdateMemberRole;

public sealed class UpdateMemberRoleHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<UpdateMemberRoleCommand, WorkspaceMemberDto>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<WorkspaceMemberDto> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Check requesting user has Admin or Owner role
        var requestingMember = await _memberRepository.GetAsync(request.WorkspaceId, request.RequestingUserId, cancellationToken);
        if (requestingMember == null || requestingMember.LeftAt != null ||
            (requestingMember.Role != WorkspaceRole.Owner && requestingMember.Role != WorkspaceRole.Admin))
        {
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.RequestingUserId, "Admin role required");
        }

        // Get target member
        var targetMember = await _memberRepository.GetAsync(request.WorkspaceId, request.TargetUserId, cancellationToken)
            ?? throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.TargetUserId);

        if (targetMember.LeftAt != null)
            throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.TargetUserId);

        // Cannot change Owner's role
        if (targetMember.Role == WorkspaceRole.Owner)
            throw new ValidationException("Cannot change owner's role. Transfer ownership first.", nameof(request.TargetUserId));

        // Cannot assign Owner role
        if (request.NewRole == WorkspaceRole.Owner)
            throw new ValidationException("Cannot assign Owner role. Use ownership transfer.", nameof(request.NewRole));

        // Only Owner can promote to Admin or demote Admins
        if ((request.NewRole == WorkspaceRole.Admin || targetMember.Role == WorkspaceRole.Admin) &&
            requestingMember.Role != WorkspaceRole.Owner)
        {
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.RequestingUserId, "Only owner can manage admin roles");
        }

        targetMember.Role = request.NewRole;
        await _memberRepository.UpdateAsync(targetMember, cancellationToken);

        return WorkspaceMemberDto.FromEntity(targetMember);
    }
}
