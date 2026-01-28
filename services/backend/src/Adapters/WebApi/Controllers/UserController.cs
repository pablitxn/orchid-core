using System.Security.Claims;
using Application.UseCases.User.GetMfaStatus;
using Application.UseCases.User.GetUser;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController(IMediator mediator) : ControllerBase
{
    private readonly IMediator _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> GetUser()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized();

        var result = await _mediator.Send(new GetUserQuery(email));

        if (result == null!)
            // todo: fix me
            // mock result
            return Ok();

        return Ok(new
        {
            result.Id,
            result.Email,
            result.Name
        });
    }

    /// <summary>
    /// Gets the MFA status for the authenticated user.
    /// </summary>
    [HttpGet("mfa")]
    public async Task<IActionResult> GetMfaStatus()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var result = await _mediator.Send(new GetMfaStatusQuery(userId));

        return Ok(new
        {
            result.Enabled,
            result.Method,
            result.EnrolledAt,
            result.LastVerifiedAt
        });
    }
}