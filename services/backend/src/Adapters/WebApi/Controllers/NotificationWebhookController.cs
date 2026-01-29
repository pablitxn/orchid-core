using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Controller for receiving notification requests from external services (e.g., romy-bot)
/// </summary>
[ApiController]
[Route("api/webhooks/notifications")]
public class NotificationWebhookController : ControllerBase
{
    private readonly INotificationScheduler _scheduler;
    private readonly INotificationService _notificationService;
    private readonly IUserRepository _userRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationWebhookController> _logger;

    public NotificationWebhookController(
        INotificationScheduler scheduler,
        INotificationService notificationService,
        IUserRepository userRepository,
        IConfiguration configuration,
        ILogger<NotificationWebhookController> logger)
    {
        _scheduler = scheduler;
        _notificationService = notificationService;
        _userRepository = userRepository;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Receive immediate notification request from external service
    /// </summary>
    /// <response code="200">Notification sent successfully</response>
    /// <response code="400">Invalid request payload</response>
    /// <response code="401">Invalid webhook signature</response>
    /// <response code="404">User not found</response>
    [HttpPost("send")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebhookSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SendNotification(CancellationToken ct)
    {
        var (payload, isValid) = await ValidateAndExtractPayloadAsync(ct);
        if (!isValid || payload == null)
            return Unauthorized("Invalid webhook signature");

        try
        {
            var request = JsonSerializer.Deserialize<SendNotificationRequest>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || request.UserId == Guid.Empty)
                return BadRequest("Invalid request payload");

            var user = await _userRepository.GetByIdAsync(request.UserId, ct);
            if (user == null)
                return NotFound($"User {request.UserId} not found");

            await _notificationService.SendNotificationAsync(
                request.UserId,
                request.Type,
                request.Title,
                request.Message,
                request.Priority,
                request.ActionUrl,
                request.ActionText,
                request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
                ct);

            _logger.LogInformation(
                "Processed immediate notification from {Source} for user {UserId}",
                request.SourceService, request.UserId);

            return Ok(new WebhookSuccessResponse(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process notification webhook");
            return StatusCode(500, "Failed to process notification");
        }
    }

    /// <summary>
    /// Schedule a notification for future delivery
    /// </summary>
    /// <response code="200">Notification scheduled successfully</response>
    /// <response code="400">Invalid request payload</response>
    /// <response code="401">Invalid webhook signature</response>
    /// <response code="404">User not found</response>
    [HttpPost("schedule")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ScheduleSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ScheduleNotification(CancellationToken ct)
    {
        var (payload, isValid) = await ValidateAndExtractPayloadAsync(ct);
        if (!isValid || payload == null)
            return Unauthorized("Invalid webhook signature");

        try
        {
            var request = JsonSerializer.Deserialize<ScheduleNotificationRequest>(payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request == null || request.UserId == Guid.Empty)
                return BadRequest("Invalid request payload");

            var user = await _userRepository.GetByIdAsync(request.UserId, ct);
            if (user == null)
                return NotFound($"User {request.UserId} not found");

            Guid notificationId;

            if (!string.IsNullOrEmpty(request.CronExpression))
            {
                notificationId = await _scheduler.ScheduleRecurringAsync(
                    request.UserId,
                    request.Type,
                    request.Title,
                    request.Message,
                    request.CronExpression,
                    request.Priority,
                    request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
                    ct);
            }
            else
            {
                notificationId = await _scheduler.ScheduleAsync(
                    request.UserId,
                    request.Type,
                    request.Title,
                    request.Message,
                    request.ScheduledFor ?? DateTime.UtcNow,
                    request.Priority,
                    request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
                    request.SourceService,
                    request.SourceId,
                    ct);
            }

            _logger.LogInformation(
                "Scheduled notification {NotificationId} from {Source} for user {UserId}",
                notificationId, request.SourceService, request.UserId);

            return Ok(new ScheduleSuccessResponse(true, notificationId));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule notification");
            return StatusCode(500, "Failed to schedule notification");
        }
    }

    /// <summary>
    /// Cancel a scheduled notification
    /// </summary>
    /// <param name="notificationId">The ID of the notification to cancel</param>
    /// <param name="ct">Cancellation token</param>
    /// <response code="200">Notification cancelled successfully</response>
    /// <response code="401">Invalid webhook signature</response>
    [HttpDelete("scheduled/{notificationId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(WebhookSuccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CancelScheduledNotification(Guid notificationId, CancellationToken ct)
    {
        var signature = Request.Headers["X-Webhook-Signature"].ToString();
        var webhookSecret = _configuration["NotificationWebhook:Secret"];

        if (string.IsNullOrEmpty(webhookSecret) || !ValidateSignatureForDelete(notificationId, signature, webhookSecret))
            return Unauthorized("Invalid webhook signature");

        try
        {
            await _scheduler.CancelAsync(notificationId, ct);
            return Ok(new WebhookSuccessResponse(true));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel notification {NotificationId}", notificationId);
            return StatusCode(500, "Failed to cancel notification");
        }
    }

    private async Task<(string? payload, bool isValid)> ValidateAndExtractPayloadAsync(CancellationToken ct)
    {
        string payload;
        using (var reader = new StreamReader(HttpContext.Request.Body))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        var signature = Request.Headers["X-Webhook-Signature"].ToString();
        var webhookSecret = _configuration["NotificationWebhook:Secret"];

        if (string.IsNullOrEmpty(webhookSecret))
        {
            _logger.LogWarning("Notification webhook secret not configured");
            return (null, false);
        }

        if (!ValidateSignature(payload, signature, webhookSecret))
        {
            _logger.LogWarning("Invalid webhook signature from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);
            return (null, false);
        }

        return (payload, true);
    }

    private static bool ValidateSignature(string payload, string signature, string secret)
    {
        if (string.IsNullOrEmpty(signature)) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = Convert.ToHexString(hash).ToLowerInvariant();
        var providedSignature = signature.Replace("sha256=", "").ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature));
    }

    private static bool ValidateSignatureForDelete(Guid notificationId, string signature, string secret)
    {
        return ValidateSignature(notificationId.ToString(), signature, secret);
    }
}

public record SendNotificationRequest
{
    public Guid UserId { get; init; }
    public string Type { get; init; } = "reminder";
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public int Priority { get; init; } = 1;
    public string? ActionUrl { get; init; }
    public string? ActionText { get; init; }
    public object? Metadata { get; init; }
    public string? SourceService { get; init; }
}

public record ScheduleNotificationRequest : SendNotificationRequest
{
    public DateTime? ScheduledFor { get; init; }
    public string? CronExpression { get; init; }
    public string? SourceId { get; init; }
}

public record WebhookSuccessResponse(bool Success);

public record ScheduleSuccessResponse(bool Success, Guid NotificationId);
