using System.Security.Claims;
using Application.UseCases.Matrix.GenerateLinkCode;
using Application.UseCases.Matrix.GetUserLink;
using Application.UseCases.Matrix.SendMessage;
using Application.UseCases.Matrix.ValidateLinkCode;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Authorization;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MatrixController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    /// <summary>
    /// Generates a verification code for linking a Matrix account to the authenticated user.
    /// </summary>
    /// <returns>The verification code and its expiration time.</returns>
    /// <response code="200">Returns the generated code</response>
    /// <response code="400">User already has a linked Matrix account</response>
    /// <response code="401">User is not authenticated</response>
    [HttpPost("link/generate")]
    [ProducesResponseType(typeof(GenerateLinkCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AlreadyLinkedResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GenerateLinkCode(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GenerateMatrixLinkCodeCommand(userId), cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new AlreadyLinkedResponse(result.ErrorCode!, result.MatrixUserId!));
        }

        return Ok(new GenerateLinkCodeResponse(result.Code!, result.ExpiresAt!.Value));
    }

    /// <summary>
    /// Validates a link code sent by a Matrix user and creates the account binding.
    /// This endpoint is intended for the Matrix bridge service.
    /// </summary>
    /// <param name="request">The Matrix user ID and verification code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success with user info or error code</returns>
    /// <response code="200">Returns success status with user info or error code</response>
    /// <response code="401">Service token is missing or invalid</response>
    /// <response code="403">Service token does not have matrix:write scope</response>
    [HttpPost("link/validate")]
    [RequireScope("matrix:write")]
    [ProducesResponseType(typeof(ValidateLinkCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidateLinkCode(
        [FromBody] ValidateLinkCodeRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ValidateMatrixLinkCodeCommand(request.MatrixUserId, request.Code),
            cancellationToken);

        return Ok(new ValidateLinkCodeResponse(
            result.IsSuccess,
            result.UserId,
            result.UserName,
            result.ErrorCode));
    }

    /// <summary>
    /// Checks if a Matrix user is linked to an Orchid account.
    /// This endpoint is intended for the Matrix bridge service.
    /// </summary>
    /// <param name="matrixUserId">The Matrix user ID (e.g., @user:matrix.orchid-labs.xyz)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Link status and user info if linked</returns>
    /// <response code="200">Returns link status</response>
    /// <response code="401">Service token is missing or invalid</response>
    /// <response code="403">Service token does not have matrix:read scope</response>
    [HttpGet("user")]
    [RequireScope("matrix:read")]
    [ProducesResponseType(typeof(GetUserLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetUserLink(
        [FromQuery] string matrixUserId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetMatrixUserLinkQuery(matrixUserId),
            cancellationToken);

        return Ok(new GetUserLinkResponse(
            result.Linked,
            result.UserId,
            result.UserName));
    }

    /// <summary>
    /// Sends a message from a Matrix user to an Orchid agent.
    /// This endpoint is intended for the Matrix bridge service.
    /// </summary>
    /// <param name="request">The message details including Matrix user, agent, room, and content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response with agent reply or error information</returns>
    /// <response code="200">Returns success with agent response or structured error</response>
    /// <response code="401">Service token is missing or invalid</response>
    /// <response code="403">Service token does not have matrix:write scope</response>
    /// <response code="429">Rate limit exceeded (also returned in body with success=false)</response>
    [HttpPost("message")]
    [RequireScope("matrix:write")]
    [ProducesResponseType(typeof(SendMatrixMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(SendMatrixMessageResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendMessage(
        [FromBody] SendMatrixMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SendMatrixMessageCommand(
                request.MatrixUserId,
                request.AgentId,
                request.RoomId,
                request.Content,
                request.ReplyToEventId,
                request.TriggerType),
            cancellationToken);

        var response = new SendMatrixMessageResponse(
            result.Success,
            result.Response,
            result.SessionId,
            result.CreditsUsed,
            result.CreditsRemaining,
            result.Error,
            result.Message,
            result.RetryAfter);

        // Return 429 status code for rate limited requests
        if (result.Error == "RATE_LIMITED")
        {
            return StatusCode(StatusCodes.Status429TooManyRequests, response);
        }

        return Ok(response);
    }
}

public record GenerateLinkCodeResponse(string Code, DateTime ExpiresAt);

public record AlreadyLinkedResponse(string Error, string MatrixUserId);

public record ValidateLinkCodeRequest(string MatrixUserId, string Code);

public record ValidateLinkCodeResponse(
    bool Success,
    Guid? UserId = null,
    string? UserName = null,
    string? Error = null);

public record GetUserLinkResponse(
    bool Linked,
    Guid? UserId = null,
    string? UserName = null);

public record SendMatrixMessageRequest(
    string MatrixUserId,
    Guid AgentId,
    string RoomId,
    string Content,
    string? ReplyToEventId = null,
    string? TriggerType = null);

public record SendMatrixMessageResponse(
    bool Success,
    string? Response = null,
    string? SessionId = null,
    int? CreditsUsed = null,
    int? CreditsRemaining = null,
    string? Error = null,
    string? Message = null,
    int? RetryAfter = null);
