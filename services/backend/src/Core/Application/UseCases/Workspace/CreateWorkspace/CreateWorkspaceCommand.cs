using Application.UseCases.Workspace.Common;
using MediatR;

namespace Application.UseCases.Workspace.CreateWorkspace;

public sealed record CreateWorkspaceCommand(
    string Name,
    string? Description,
    string? AvatarUrl,
    string? Slug,
    Guid UserId
) : IRequest<WorkspaceDto>;
