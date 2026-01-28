using Application.Interfaces;
using Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Auth.ValidateLegacyCredentials;

/// <summary>
/// Handler for validating legacy credentials for Auth0 Custom Database Connection migration.
/// </summary>
public sealed class ValidateLegacyCredentialsHandler(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ILogger<ValidateLegacyCredentialsHandler> logger)
    : IRequestHandler<ValidateLegacyCredentialsCommand, LegacyCredentialValidationDto>
{
    public async Task<LegacyCredentialValidationDto> Handle(
        ValidateLegacyCredentialsCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await userRepository.GetByEmailAsync(normalizedEmail);
        if (user == null)
        {
            logger.LogDebug("Auth0 migration: User not found for email");
            return new LegacyCredentialValidationDto(
                false, null, null, null, null, "User not found");
        }

        if (user.Auth0MigrationStatus == Auth0MigrationStatus.Completed)
        {
            logger.LogDebug("Auth0 migration: User {UserId} already migrated", user.Id);
            return new LegacyCredentialValidationDto(
                false, null, null, null, null, "User already migrated to Auth0");
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            logger.LogDebug("Auth0 migration: User {UserId} has no password (OAuth only)", user.Id);
            return new LegacyCredentialValidationDto(
                false, null, null, null, null, "User has no legacy password");
        }

        if (!passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
        {
            logger.LogWarning("Auth0 migration: Invalid password for user {UserId}", user.Id);
            return new LegacyCredentialValidationDto(
                false, null, null, null, null, "Invalid credentials");
        }

        logger.LogInformation("Auth0 migration: Credentials validated for user {UserId}", user.Id);

        return new LegacyCredentialValidationDto(
            true,
            user.Id,
            user.Email,
            user.Name,
            user.AvatarUrl,
            null);
    }
}
