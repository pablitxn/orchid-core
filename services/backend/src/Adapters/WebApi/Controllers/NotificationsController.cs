using System.Security.Claims;
using Application.Interfaces;
using Application.UseCases.NotificationPreferences;
using Application.UseCases.NotificationPreferences.GetNotificationPreferences;
using Application.UseCases.NotificationPreferences.UpdateNotificationPreferences;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// User-facing notification management endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IScheduledNotificationRepository _scheduledRepository;

    public NotificationsController(
        IMediator mediator,
        IScheduledNotificationRepository scheduledRepository)
    {
        _mediator = mediator;
        _scheduledRepository = scheduledRepository;
    }

    /// <summary>
    /// Get user's notification preferences
    /// </summary>
    /// <response code="200">Returns the user's notification preferences</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationPreferencesDto>> GetPreferences(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _mediator.Send(new GetNotificationPreferencesQuery(userId.Value), ct);
        return Ok(result);
    }

    /// <summary>
    /// Update user's notification preferences
    /// </summary>
    /// <param name="request">The preferences to update</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Returns the updated preferences</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(NotificationPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var result = await _mediator.Send(new UpdateNotificationPreferencesCommand(
            userId.Value,
            request.NotificationsEnabled,
            request.QuietHoursStart,
            request.QuietHoursEnd,
            request.Timezone,
            request.EnabledTypes,
            request.MatrixNotificationsEnabled,
            request.EmailNotificationsEnabled,
            request.EmailDigestFrequency), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get user's scheduled notifications
    /// </summary>
    /// <response code="200">Returns the user's pending scheduled notifications</response>
    /// <response code="401">User is not authenticated</response>
    [HttpGet("scheduled")]
    [ProducesResponseType(typeof(IEnumerable<ScheduledNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<ScheduledNotificationDto>>> GetScheduled(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var notifications = await _scheduledRepository.GetByUserIdAsync(userId.Value, ct);

        var dtos = notifications.Select(n => new ScheduledNotificationDto(
            n.Id,
            n.Type,
            n.Title,
            n.Message,
            n.ScheduledFor,
            n.IsRecurring,
            n.CronExpression));

        return Ok(dtos);
    }

    private Guid? GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }
}

public record UpdateNotificationPreferencesRequest(
    bool? NotificationsEnabled,
    TimeOnly? QuietHoursStart,
    TimeOnly? QuietHoursEnd,
    string? Timezone,
    string[]? EnabledTypes,
    bool? MatrixNotificationsEnabled,
    bool? EmailNotificationsEnabled,
    string? EmailDigestFrequency);

public record ScheduledNotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    DateTime ScheduledFor,
    bool IsRecurring,
    string? CronExpression);
