using Application.UseCases.Workspace.Common;
using MediatR;

namespace Application.UseCases.Workspace.UpdateWorkspace;

public sealed record UpdateWorkspaceCommand(
    Guid WorkspaceId,
    Guid UserId,
    string? Name,
    string? Description,
    string? AvatarUrl,
    string? Slug
) : IRequest<WorkspaceDto>;
