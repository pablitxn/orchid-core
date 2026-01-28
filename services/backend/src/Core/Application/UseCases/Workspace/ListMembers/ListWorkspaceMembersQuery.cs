using Application.UseCases.Workspace.Common;
using MediatR;

namespace Application.UseCases.Workspace.ListMembers;

public sealed record ListWorkspaceMembersQuery(
    Guid WorkspaceId,
    Guid RequestingUserId
) : IRequest<List<WorkspaceMemberDto>>;
