using Application.UseCases.PluginReview.Common;
using Application.UseCases.PluginReview.CreatePluginReview;
using Application.UseCases.PluginReview.DeletePluginReview;
using Application.UseCases.PluginReview.FlagPluginReview;
using Application.UseCases.PluginReview.ListPluginReviews;
using Application.UseCases.PluginReview.UpdatePluginReview;
using Application.UseCases.PluginReview.VoteHelpful;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

[ApiController]
[Route("api/marketplace/plugins/{pluginId:guid}/reviews")]
[Authorize]
public class PluginReviewsController(IMediator mediator, ILogger<PluginReviewsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> CreateReview(
        Guid pluginId,
        [FromBody] CreatePluginReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new CreatePluginReviewCommand(
                pluginId,
                userId,
                request.Rating,
                request.Title,
                request.Body);
            var result = await mediator.Send(command, ct);
            return CreatedAtAction(nameof(ListReviews), new { pluginId }, result);
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (PluginNotOwnedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (DuplicateReviewException ex)
        {
            return Conflict(new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{reviewId:guid}")]
    public async Task<IActionResult> UpdateReview(
        Guid pluginId,
        Guid reviewId,
        [FromBody] UpdatePluginReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new UpdatePluginReviewCommand(
                reviewId,
                userId,
                request.Rating,
                request.Title,
                request.Body);
            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (PluginReviewNotFoundException)
        {
            return NotFound(new { error = "Review not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{reviewId:guid}")]
    public async Task<IActionResult> DeleteReview(
        Guid pluginId,
        Guid reviewId,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new DeletePluginReviewCommand(reviewId, userId);
            await mediator.Send(command, ct);
            return NoContent();
        }
        catch (PluginReviewNotFoundException)
        {
            return NotFound(new { error = "Review not found" });
        }
        catch (UnauthorizedDomainAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ListReviews(
        Guid pluginId,
        [FromQuery] string sortBy = "newest",
        [FromQuery] int page = 1,
        [FromQuery] int limit = 10,
        CancellationToken ct = default)
    {
        var sortByEnum = Enum.TryParse<PluginReviewSortBy>(sortBy, true, out var sort)
            ? sort
            : PluginReviewSortBy.Newest;

        var query = new ListPluginReviewsQuery
        {
            PluginId = pluginId,
            SortBy = sortByEnum,
            Page = page,
            PageSize = limit
        };
        var result = await mediator.Send(query, ct);
        return Ok(result);
    }
}

[ApiController]
[Route("api/marketplace/reviews")]
[Authorize]
public class ReviewModerationController(IMediator mediator, ILogger<ReviewModerationController> logger) : ControllerBase
{
    [HttpPost("{reviewId:guid}/flag")]
    public async Task<IActionResult> FlagReview(
        Guid reviewId,
        [FromBody] FlagReviewRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new FlagPluginReviewCommand(reviewId, userId, request.Reason, request.Details);
            await mediator.Send(command, ct);
            return Ok(new { message = "Review flagged for moderation" });
        }
        catch (PluginReviewNotFoundException)
        {
            return NotFound(new { error = "Review not found" });
        }
        catch (DuplicateReviewFlagException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPost("{reviewId:guid}/helpful")]
    public async Task<IActionResult> VoteHelpful(Guid reviewId, CancellationToken ct)
    {
        try
        {
            var userId = User.GetUserId();
            var command = new VoteHelpfulCommand(reviewId, userId);
            var result = await mediator.Send(command, ct);
            return Ok(result);
        }
        catch (PluginReviewNotFoundException)
        {
            return NotFound(new { error = "Review not found" });
        }
    }
}

// Request DTOs
public sealed record CreatePluginReviewRequest(int Rating, string? Title, string? Body);
public sealed record UpdatePluginReviewRequest(int Rating, string? Title, string? Body);
public sealed record FlagReviewRequest(string Reason, string? Details);
