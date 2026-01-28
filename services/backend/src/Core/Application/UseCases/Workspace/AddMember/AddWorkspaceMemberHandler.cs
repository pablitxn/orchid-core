using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.AddMember;

public sealed class AddWorkspaceMemberHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository,
    IUserRepository userRepository) : IRequestHandler<AddWorkspaceMemberCommand, WorkspaceMemberDto>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<WorkspaceMemberDto> Handle(AddWorkspaceMemberCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Cannot add members to personal workspace
        if (workspace.IsPersonal)
            throw new ValidationException("Cannot add members to personal workspace.", nameof(request.WorkspaceId));

        // Check inviting user has Admin or Owner role
        var hasPermission = await _memberRepository.HasRoleAsync(
            request.WorkspaceId, request.InvitingUserId, WorkspaceRole.Admin, cancellationToken);

        if (!hasPermission)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.InvitingUserId, "Admin role required");

        // Cannot assign Owner role
        if (request.Role == WorkspaceRole.Owner)
            throw new ValidationException("Cannot assign Owner role. Ownership must be transferred.", nameof(request.Role));

        // Verify invited user exists
        var invitedUser = await _userRepository.GetByIdAsync(request.InvitedUserId, cancellationToken)
            ?? throw new UserNotFoundException(request.InvitedUserId);

        // Check if already a member
        var existingMember = await _memberRepository.GetAsync(request.WorkspaceId, request.InvitedUserId, cancellationToken);
        if (existingMember != null && existingMember.LeftAt == null)
            throw new ValidationException("User is already a member of this workspace.", nameof(request.InvitedUserId));

        // Reactivate if previously left, or create new
        WorkspaceMemberEntity member;
        if (existingMember != null)
        {
            existingMember.LeftAt = null;
            existingMember.Role = request.Role;
            existingMember.InvitedById = request.InvitingUserId;
            existingMember.JoinedAt = DateTime.UtcNow;
            await _memberRepository.UpdateAsync(existingMember, cancellationToken);
            member = existingMember;
        }
        else
        {
            member = new WorkspaceMemberEntity
            {
                Id = Guid.NewGuid(),
                WorkspaceId = request.WorkspaceId,
                UserId = request.InvitedUserId,
                Role = request.Role,
                InvitedById = request.InvitingUserId,
                JoinedAt = DateTime.UtcNow
            };
            await _memberRepository.CreateAsync(member, cancellationToken);
        }

        member.User = invitedUser;
        return WorkspaceMemberDto.FromEntity(member);
    }
}
