using Application.UseCases.Workspace.Common;
using MediatR;

namespace Application.UseCases.Workspace.GetWorkspaceById;

public sealed record GetWorkspaceByIdQuery(Guid WorkspaceId, Guid UserId) : IRequest<WorkspaceDetailDto>;
