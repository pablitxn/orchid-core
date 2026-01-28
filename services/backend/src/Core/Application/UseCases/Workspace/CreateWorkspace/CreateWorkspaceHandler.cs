using System.Text.RegularExpressions;
using Application.Interfaces;
using Application.UseCases.Workspace.Common;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Workspace.CreateWorkspace;

public sealed partial class CreateWorkspaceHandler(
    IWorkspaceRepository workspaceRepository,
    IWorkspaceMemberRepository memberRepository,
    IUserRepository userRepository) : IRequestHandler<CreateWorkspaceCommand, WorkspaceDto>
{
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly IWorkspaceMemberRepository _memberRepository = memberRepository;
    private readonly IUserRepository _userRepository = userRepository;

    public async Task<WorkspaceDto> Handle(CreateWorkspaceCommand request, CancellationToken cancellationToken)
    {
        CreateWorkspaceValidator.Validate(request);

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new UserNotFoundException(request.UserId);

        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? GenerateSlug(request.Name)
            : SanitizeSlug(request.Slug);

        if (await _workspaceRepository.SlugExistsAsync(slug, cancellationToken))
        {
            throw new WorkspaceSlugAlreadyExistsException(slug);
        }

        var workspace = new WorkspaceEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            AvatarUrl = request.AvatarUrl,
            Slug = slug,
            OwnerId = request.UserId,
            IsPersonal = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _workspaceRepository.CreateAsync(workspace, cancellationToken);

        var member = new WorkspaceMemberEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.Id,
            UserId = request.UserId,
            Role = WorkspaceRole.Owner,
            JoinedAt = DateTime.UtcNow
        };

        await _memberRepository.CreateAsync(member, cancellationToken);

        workspace.Members = new List<WorkspaceMemberEntity> { member };
        return WorkspaceDto.FromEntity(workspace, WorkspaceRole.Owner);
    }

    private static string GenerateSlug(string name)
    {
        var slug = SlugRegex().Replace(name.ToLowerInvariant(), "-").Trim('-');
        return $"{slug}-{Guid.NewGuid():N}"[..Math.Min(slug.Length + 7, 100)];
    }

    private static string SanitizeSlug(string slug)
    {
        return SlugRegex().Replace(slug.ToLowerInvariant(), "-").Trim('-');
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
