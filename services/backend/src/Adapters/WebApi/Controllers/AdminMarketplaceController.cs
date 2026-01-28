using Application.UseCases.Admin.ApproveSubmission;
using Application.UseCases.Admin.ListSubmissions;
using Application.UseCases.Admin.RejectSubmission;
using Domain.Enums;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/admin/marketplace")]
[Authorize(Roles = "admin")]
public class AdminMarketplaceController(IMediator mediator, ILogger<AdminMarketplaceController> logger) : ControllerBase
{
    /// <summary>
    /// List all plugin submissions (pending by default)
    /// </summary>
    [HttpGet("submissions")]
    public async Task<IActionResult> ListSubmissions(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        SubmissionStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<SubmissionStatus>(status, true, out var parsed))
        {
            statusFilter = parsed;
        }

        var query = new ListSubmissionsQuery(statusFilter);
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Approve a plugin submission
    /// </summary>
    [HttpPost("submissions/{submissionId:guid}/approve")]
    public async Task<IActionResult> ApproveSubmission(
        Guid submissionId,
        [FromBody] ApproveSubmissionRequest? request,
        CancellationToken ct)
    {
        try
        {
            var adminId = User.GetUserId();
            var command = new ApproveSubmissionCommand(
                submissionId,
                adminId,
                request?.ReviewNotes);
            var result = await mediator.Send(command, ct);

            logger.LogInformation("Submission {SubmissionId} approved by admin {AdminId}. Plugin: {PluginId}",
                submissionId, adminId, result.PluginId);

            return Ok(result);
        }
        catch (SubmissionNotFoundException)
        {
            return NotFound(new { error = "Submission not found" });
        }
        catch (InvalidSubmissionStatusException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Associated plugin not found" });
        }
    }

    /// <summary>
    /// Reject a plugin submission
    /// </summary>
    [HttpPost("submissions/{submissionId:guid}/reject")]
    public async Task<IActionResult> RejectSubmission(
        Guid submissionId,
        [FromBody] RejectSubmissionRequest request,
        CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new { error = "Rejection reason is required" });
            }

            var adminId = User.GetUserId();
            var command = new RejectSubmissionCommand(
                submissionId,
                adminId,
                request.Reason);
            var result = await mediator.Send(command, ct);

            logger.LogInformation("Submission {SubmissionId} rejected by admin {AdminId}. Reason: {Reason}",
                submissionId, adminId, request.Reason);

            return Ok(result);
        }
        catch (SubmissionNotFoundException)
        {
            return NotFound(new { error = "Submission not found" });
        }
        catch (InvalidSubmissionStatusException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

// Request DTOs
public sealed record ApproveSubmissionRequest(string? ReviewNotes);
public sealed record RejectSubmissionRequest(string Reason);
