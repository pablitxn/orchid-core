using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.ListUserWorkspaces;

public sealed class ListUserWorkspacesHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository,
    IUserRepository userRepository) : IRequestHandler<ListUserWorkspacesQuery, List<WorkspaceDto>>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<List<WorkspaceDto>> Handle(ListUserWorkspacesQuery request, CancellationToken cancellationToken)
    {
        var workspaces = await _workspaceRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        // Auto-create personal workspace if none exist
        if (workspaces.Count == 0)
        {
            var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new UserNotFoundException(request.UserId);

            var personalWorkspace = await CreatePersonalWorkspaceAsync(user, cancellationToken);
            workspaces = new List<WorkspaceEntity> { personalWorkspace };
        }

        // Get user's role in each workspace
        var memberships = await _memberRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var roleMap = memberships.ToDictionary(m => m.WorkspaceId, m => m.Role);

        return workspaces
            .Select(w => WorkspaceDto.FromEntity(w, roleMap.GetValueOrDefault(w.Id)))
            .ToList();
    }

    private async Task<WorkspaceEntity> CreatePersonalWorkspaceAsync(UserEntity user, CancellationToken cancellationToken)
    {
        var workspace = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            Name = $"{user.Name ?? user.Email ?? "My"}'s Workspace",
            Slug = $"personal-{user.Id:N}",
            OwnerId = user.Id,
            IsPersonal = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _workspaceRepository.CreateAsync(workspace, cancellationToken);

        var member = new WorkspaceMemberEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = user.Id,
            Role = WorkspaceRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        await _memberRepository.CreateAsync(member, cancellationToken);

        workspace.Members = new List<WorkspaceMemberEntity> { member };
        return workspace;
    }
}
