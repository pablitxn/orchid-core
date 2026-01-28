using Application.Interfaces;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.RemoveMember;

public sealed class RemoveWorkspaceMemberHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<RemoveWorkspaceMemberCommand, Unit>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<Unit> Handle(RemoveWorkspaceMemberCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Check requesting user has Admin or Owner role
        var hasPermission = await _memberRepository.HasRoleAsync(
            request.WorkspaceId, request.RequestingUserId, WorkspaceRole.Admin, cancellationToken);

        if (!hasPermission)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.RequestingUserId, "Admin role required");

        // Get target member
        var targetMember = await _memberRepository.GetAsync(request.WorkspaceId, request.TargetUserId, cancellationToken)
            ?? throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.TargetUserId);

        if (targetMember.LeftAt != null)
            throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.TargetUserId);

        // Cannot remove Owner
        if (targetMember.Role == WorkspaceRole.Owner)
            throw new ValidationException("Cannot remove workspace owner.", nameof(request.TargetUserId));

        // Admin cannot remove other Admins (only Owner can)
        var requestingMember = await _memberRepository.GetAsync(request.WorkspaceId, request.RequestingUserId, cancellationToken);
        if (targetMember.Role == WorkspaceRole.Admin && requestingMember?.Role != WorkspaceRole.Owner)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.RequestingUserId, "Only owner can remove admins");

        // Soft remove
        targetMember.LeftAt = DateTime.UtcNow;
        await _memberRepository.UpdateAsync(targetMember, cancellationToken);

        return Unit.Value;
    }
}
