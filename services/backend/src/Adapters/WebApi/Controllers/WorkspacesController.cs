using Application.UseCases.Workspace.AddMember;
using Application.UseCases.Workspace.Common;
using Application.UseCases.Workspace.CreateWorkspace;
using Application.UseCases.Workspace.DeleteWorkspace;
using Application.UseCases.Workspace.GetWorkspaceById;
using Application.UseCases.Workspace.LeaveWorkspace;
using Application.UseCases.Workspace.ListMembers;
using Application.UseCases.Workspace.ListUserWorkspaces;
using Application.UseCases.Workspace.RemoveMember;
using Application.UseCases.Workspace.UpdateMemberRole;
using Application.UseCases.Workspace.UpdateWorkspace;
using Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

public class CreateWorkspaceRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Slug { get; set; }
}

public class UpdateWorkspaceRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Slug { get; set; }
}

public class AddMemberRequest
{
    public Guid UserId { get; set; }
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;
}

public class UpdateMemberRoleRequest
{
    public WorkspaceRole Role { get; set; }
}

[ApiController]
[Route("api/workspaces")]
[Authorize]
public class WorkspacesController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Get all workspaces the current user belongs to.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspaces()
    {
        var userId = User.GetUserId();
        var workspaces = await _mediator.Send(new ListUserWorkspacesQuery(userId));
        return Ok(workspaces);
    }

    /// <summary>
    /// Get workspace details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspace(Guid id)
    {
        var userId = User.GetUserId();
        var workspace = await _mediator.Send(new GetWorkspaceByIdQuery(id, userId));
        return Ok(workspace);
    }

    /// <summary>
    /// Create a new workspace.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWorkspace([FromBody] CreateWorkspaceRequest request)
    {
        var userId = User.GetUserId();
        var command = new CreateWorkspaceCommand(
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.Slug,
            userId
        );

        var workspace = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetWorkspace), new { id = workspace.Id }, workspace);
    }

    /// <summary>
    /// Update workspace details (Admin+ required).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(WorkspaceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateWorkspace(Guid id, [FromBody] UpdateWorkspaceRequest request)
    {
        var userId = User.GetUserId();
        var command = new UpdateWorkspaceCommand(
            id,
            userId,
            request.Name,
            request.Description,
            request.AvatarUrl,
            request.Slug
        );

        var workspace = await _mediator.Send(command);
        return Ok(workspace);
    }

    /// <summary>
    /// Delete a workspace (Owner only).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspace(Guid id)
    {
        var userId = User.GetUserId();
        await _mediator.Send(new DeleteWorkspaceCommand(id, userId));
        return NoContent();
    }

    /// <summary>
    /// Get workspace members.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(List<WorkspaceMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var userId = User.GetUserId();
        var members = await _mediator.Send(new ListWorkspaceMembersQuery(id, userId));
        return Ok(members);
    }

    /// <summary>
    /// Add a member to the workspace (Admin+ required).
    /// </summary>
    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(typeof(WorkspaceMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddMemberRequest request)
    {
        var userId = User.GetUserId();
        var command = new AddWorkspaceMemberCommand(id, request.UserId, request.Role, userId);
        var member = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetMembers), new { id }, member);
    }

    /// <summary>
    /// Update a member's role (Admin+ required, cannot change Owner).
    /// </summary>
    [HttpPut("{id:guid}/members/{memberId:guid}/role")]
    [ProducesResponseType(typeof(WorkspaceMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid memberId, [FromBody] UpdateMemberRoleRequest request)
    {
        var userId = User.GetUserId();
        var command = new UpdateMemberRoleCommand(id, memberId, request.Role, userId);
        var member = await _mediator.Send(command);
        return Ok(member);
    }

    /// <summary>
    /// Remove a member from the workspace (Admin+ required, cannot remove Owner).
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberId)
    {
        var userId = User.GetUserId();
        await _mediator.Send(new RemoveWorkspaceMemberCommand(id, memberId, userId));
        return NoContent();
    }

    /// <summary>
    /// Leave a workspace (self-removal, not for owners).
    /// </summary>
    [HttpPost("{id:guid}/leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LeaveWorkspace(Guid id)
    {
        var userId = User.GetUserId();
        await _mediator.Send(new LeaveWorkspaceCommand(id, userId));
        return NoContent();
    }
}
