using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Auth.SyncAuth0User;

/// <summary>
/// Command to synchronize an Auth0 user with the local database.
/// Called from webhook or middleware.
/// </summary>
public record SyncAuth0UserCommand(
    string Auth0UserId,
    string Email,
    string? Name,
    string? AvatarUrl,
    string? Connection,
    IReadOnlyList<string> Roles) : IRequest<Auth0SyncResult>;
