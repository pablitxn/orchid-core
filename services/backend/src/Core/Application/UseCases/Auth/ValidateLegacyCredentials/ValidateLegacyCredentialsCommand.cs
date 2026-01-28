using MediatR;

namespace Application.UseCases.Auth.ValidateLegacyCredentials;

/// <summary>
/// Command to validate legacy credentials for Auth0 Custom Database Connection.
/// </summary>
public sealed record ValidateLegacyCredentialsCommand(
    string Email,
    string Password) : IRequest<LegacyCredentialValidationDto>;

/// <summary>
/// Result of legacy credential validation for Auth0 Custom DB.
/// </summary>
public sealed record LegacyCredentialValidationDto(
    bool IsValid,
    Guid? UserId,
    string? Email,
    string? Name,
    string? AvatarUrl,
    string? ErrorMessage);
