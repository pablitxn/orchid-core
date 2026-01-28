using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.GetWorkspaceById;

public sealed class GetWorkspaceByIdHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository,
    IWorkspaceSubscriptionRepository subscriptionRepository) : IRequestHandler<GetWorkspaceByIdQuery, WorkspaceDetailDto>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;
    private readonly IWorkspaceSubscriptionRepository _subscriptionRepository = subscriptionRepository;

    public async Task<WorkspaceDetailDto> Handle(GetWorkspaceByIdQuery request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdWithMembersAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        var membership = await _memberRepository.GetAsync(request.WorkspaceId, request.UserId, cancellationToken);
        if (membership == null || membership.LeftAt != null)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.UserId);

        var subscription = await _subscriptionRepository.GetByWorkspaceIdAsync(request.WorkspaceId, cancellationToken);

        return WorkspaceDetailDto.FromEntity(workspace, membership.Role, subscription);
    }
}
