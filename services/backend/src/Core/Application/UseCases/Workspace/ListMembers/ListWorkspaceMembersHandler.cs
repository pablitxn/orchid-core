using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.ListMembers;

public sealed class ListWorkspaceMembersHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<ListWorkspaceMembersQuery, List<WorkspaceMemberDto>>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<List<WorkspaceMemberDto>> Handle(ListWorkspaceMembersQuery request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Verify requesting user is a member
        var isMember = await _memberRepository.IsMemberAsync(request.WorkspaceId, request.RequestingUserId, cancellationToken);
        if (!isMember)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.RequestingUserId);

        var members = await _memberRepository.GetByWorkspaceIdAsync(request.WorkspaceId, cancellationToken);

        return members
            .Where(m => m.LeftAt == null)
            .Select(WorkspaceMemberDto.FromEntity)
            .ToList();
    }
}
