using Application.UseCases.Workspace.Common;
using MediatR;

namespace Application.UseCases.Workspace.ListUserWorkspaces;

public sealed record ListUserWorkspacesQuery(Guid UserId) : IRequest<List<WorkspaceDto>>;
