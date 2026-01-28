using Application.UseCases.Social.GetUserSocialIdentities;
using Application.UseCases.Social.UnlinkSocialIdentity;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Controller for managing user social identities (Google, GitHub, etc.).
/// </summary>
[ApiController]
[Route("api/social/identities")]
[Authorize]
public class SocialIdentitiesController(
    IMediator mediator,
    ILogger<SocialIdentitiesController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all linked social identities for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetIdentities(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var result = await mediator.Send(new GetUserSocialIdentitiesQuery(userId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Unlinks a social identity from the current user.
    /// </summary>
    /// <param name="provider">The provider to unlink (e.g., "google-oauth2", "github")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpDelete("{provider}")]
    public async Task<IActionResult> UnlinkIdentity(string provider, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();

        logger.LogInformation(
            "User {UserId} requesting to unlink social identity {Provider}",
            userId, provider);

        await mediator.Send(new UnlinkSocialIdentityCommand(userId, provider), cancellationToken);
        return NoContent();
    }
}
