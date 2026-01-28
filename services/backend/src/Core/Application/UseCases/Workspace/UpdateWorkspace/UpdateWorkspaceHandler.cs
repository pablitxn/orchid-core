using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.UpdateWorkspace;

public sealed partial class UpdateWorkspaceHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository) : IRequestHandler<UpdateWorkspaceCommand, WorkspaceDto>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;

    public async Task<WorkspaceDto> Handle(UpdateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.GetByIdWithMembersAsync(request.WorkspaceId, cancellationToken)
            ?? throw new WorkspaceNotFoundException(request.WorkspaceId);

        if (workspace.DeletedAt != null)
            throw new WorkspaceNotFoundException(request.WorkspaceId);

        // Check user has Admin or Owner role
        var hasPermission = await _memberRepository.HasRoleAsync(
            request.WorkspaceId, request.UserId, WorkspaceRole.Admin, cancellationToken);

        if (!hasPermission)
            throw new WorkspaceAccessDeniedException(request.WorkspaceId, request.UserId, "Admin role required");

        // Update fields if provided
        if (request.Name != null)
            workspace.Name = request.Name;

        if (request.Description != null)
            workspace.Description = request.Description;

        if (request.AvatarUrl != null)
            workspace.AvatarUrl = request.AvatarUrl;

        if (request.Slug != null)
        {
            var newSlug = SanitizeSlug(request.Slug);
            if (newSlug != workspace.Slug)
            {
                if (await _workspaceRepository.SlugExistsAsync(newSlug, cancellationToken))
                    throw new WorkspaceSlugAlreadyExistsException(newSlug);
                workspace.Slug = newSlug;
            }
        }

        workspace.UpdatedAt = DateTime.UtcNow;
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        var membership = await _memberRepository.GetAsync(request.WorkspaceId, request.UserId, cancellationToken);
        return WorkspaceDto.FromEntity(workspace, membership?.Role);
    }

    private static string SanitizeSlug(string slug)
    {
        return SlugRegex().Replace(slug.ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
