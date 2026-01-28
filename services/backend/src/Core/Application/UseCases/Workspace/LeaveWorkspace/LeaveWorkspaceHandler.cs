using Application.Interfaces;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.LeaveWorkspace;

public sealed class LeaveWorkspaceHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<LeaveWorkspaceCommand, Unit>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<Unit> Handle(LeaveWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        var member = await _memberRepository.GetAsync(request.WorkspaceId, request.UserId, cancellationToken)
            ?? throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.UserId);

        if (member.LeftAt != null)
            throw new WorkspaceMemberNotFoundException(request.WorkspaceId, request.UserId);

        // Owner cannot leave - must transfer ownership first
        if (member.Role == WorkspaceRole.Owner)
            throw new ValidationException("Workspace owner cannot leave. Transfer ownership first or delete the workspace.", nameof(request.UserId));

        member.LeftAt = DateTime.UtcNow;
        await _memberRepository.UpdateAsync(member, cancellationToken);

        return Unit.Value;
    }
}
