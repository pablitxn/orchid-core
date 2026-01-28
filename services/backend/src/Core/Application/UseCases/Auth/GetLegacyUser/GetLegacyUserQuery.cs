using MediatR;

namespace Application.UseCases.Auth.GetLegacyUser;

/// <summary>
/// Query to check if a user exists in the legacy database for Auth0 get_user script.
/// </summary>
public sealed record GetLegacyUserQuery(string Email) : IRequest<LegacyUserDto?>;

/// <summary>
/// Result of legacy user lookup for Auth0 Custom DB get_user script.
/// </summary>
public sealed record LegacyUserDto(
    Guid UserId,
    string Email,
    string? Name,
    string? AvatarUrl,
    bool IsMigrated);
