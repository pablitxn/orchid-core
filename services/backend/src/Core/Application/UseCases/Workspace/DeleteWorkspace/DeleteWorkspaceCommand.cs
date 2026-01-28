using MediatR;

namespace Application.UseCases.Workspace.DeleteWorkspace;

public sealed record DeleteWorkspaceCommand(Guid WorkspaceId, Guid UserId) : IRequest<Unit>;
