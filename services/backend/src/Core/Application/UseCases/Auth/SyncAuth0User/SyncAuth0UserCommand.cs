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
    IReadOnlyList<string> Roles,
    IReadOnlyList<Auth0IdentityDto> Identities,
    Auth0MfaDto? Mfa = null) : IRequest<Auth0SyncResult>;

/// <summary>
/// DTO representing an Auth0 identity from the webhook payload.
/// </summary>
public record Auth0IdentityDto(
    string Provider,
    string ProviderUserId,
    string? Email,
    string? DisplayName);

/// <summary>
/// DTO representing MFA status from Auth0.
/// </summary>
public record Auth0MfaDto(
    bool Enabled,
    string? Method);
