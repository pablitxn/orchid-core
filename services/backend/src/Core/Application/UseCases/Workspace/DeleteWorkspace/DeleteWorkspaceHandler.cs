using Application.Interfaces;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.DeleteWorkspace;

public sealed class DeleteWorkspaceHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<DeleteWorkspaceCommand, Unit>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<Unit> Handle(DeleteWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Only Owner can delete
        var membership = await _memberRepository.GetAsync(request.WorkspaceId, request.UserId, cancellationToken);
        if (membership == null || membership.Role != WorkspaceRole.Owner)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.UserId, "Owner role required");

        // Cannot delete personal workspace
        if (workspace.IsPersonal)
            throw new ValidationException("Personal workspaces cannot be deleted.", nameof(request.WorkspaceId));

        // Soft delete
        workspace.DeletedAt = DateTime.UtcNow;
        workspace.UpdatedAt = DateTime.UtcNow;
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        return Unit.Value;
    }
}
