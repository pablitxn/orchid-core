using Application.Interfaces;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Auth.GetLegacyUser;

/// <summary>
/// Handler for looking up a legacy user for Auth0 Custom DB get_user script.
/// </summary>
public sealed class GetLegacyUserHandler(IUserRepository userRepository)
    : IRequestHandler<GetLegacyUserQuery, LegacyUserDto?>
{
    public async Task<LegacyUserDto?> Handle(
        GetLegacyUserQuery request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepository.GetByEmailAsync(normalizedEmail);

        if (user == null)
            return null;

        // Only return users who have a password (exclude OAuth-only)
        if (string.IsNullOrEmpty(user.PasswordHash))
            return null;

        return new LegacyUserDto(
            user.Id,
            user.Email!,
            user.Name,
            user.AvatarUrl,
            user.Auth0MigrationStatus == Auth0MigrationStatus.Completed);
    }
}
