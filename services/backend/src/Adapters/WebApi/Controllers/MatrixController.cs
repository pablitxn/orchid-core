using System.Security.Claims;
using Application.UseCases.Matrix.GenerateLinkCode;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}

public record GenerateLinkCodeResponse(string Code, DateTime ExpiresAt);

public record AlreadyLinkedResponse(string Error, string MatrixUserId);
