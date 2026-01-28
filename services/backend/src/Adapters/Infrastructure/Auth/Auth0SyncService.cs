using Application.Interfaces;
using Application.UseCases.Auth.SyncAuth0User;
using MediatR;

namespace Infrastructure.Auth;

/// <summary>
/// Adapter implementation for Auth0 user synchronization.
/// Delegates to MediatR command handler for actual logic.
/// </summary>
public class Auth0SyncService(IMediator mediator) : IAuth0SyncService
{
    public async Task<Auth0SyncResult> SyncUserAsync(
        Auth0UserData userData,
        CancellationToken cancellationToken = default)
    {
        var command = new SyncAuth0UserCommand(
            userData.Auth0UserId,
            userData.Email,
            userData.Name,
            userData.AvatarUrl,
            userData.Connection,
            userData.Roles,
            Array.Empty<Auth0IdentityDto>());

        return await mediator.Send(command, cancellationToken);
    }
}
