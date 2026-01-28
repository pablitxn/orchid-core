using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Auth.SyncAuth0User;

public class SyncAuth0UserHandler(
    IUserRepository userRepository,
    IRoleRepository roleRepository,
    IEventPublisher eventPublisher,
    ILogger<SyncAuth0UserHandler> logger)
    : IRequestHandler<SyncAuth0UserCommand, Auth0SyncResult>
{
    public async Task<Auth0SyncResult> Handle(
        SyncAuth0UserCommand request,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var isNewUser = false;
        var wasLinked = false;
        var profileUpdated = false;
        var rolesUpdated = false;

        // Step 1: Try to find user by Auth0 ID (already linked)
        var user = await userRepository.GetByAuth0UserIdAsync(
            request.Auth0UserId, cancellationToken);

        if (user == null)
        {
            // Step 2: Try to find by email (link existing user)
            user = await userRepository.GetByEmailAsync(normalizedEmail);

            if (user != null)
            {
                // Link existing user to Auth0
                user.Auth0UserId = request.Auth0UserId;
                user.Auth0Connection = request.Connection;
                user.Auth0LastSyncedAt = DateTime.UtcNow;
                wasLinked = true;

                logger.LogInformation(
                    "Linked existing user {UserId} to Auth0 account {Auth0UserId}",
                    user.Id, request.Auth0UserId);

                await eventPublisher.PublishAsync(new Auth0UserLinkedEvent(
                    user.Id, request.Auth0UserId, request.Connection), cancellationToken);
            }
            else
            {
                // Step 3: Create new user
                user = new UserEntity
                {
                    Id = Guid.NewGuid(),
                    Email = normalizedEmail,
                    Name = request.Name ?? $"user_{Guid.NewGuid():N}",
                    AvatarUrl = request.AvatarUrl,
                    Auth0UserId = request.Auth0UserId,
                    Auth0Connection = request.Connection,
                    Auth0LastSyncedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await userRepository.CreateAsync(user);
                isNewUser = true;

                logger.LogInformation(
                    "Created new user {UserId} from Auth0 account {Auth0UserId}",
                    user.Id, request.Auth0UserId);

                await eventPublisher.PublishAsync(new UserCreatedFromAuth0Event(
                    user.Id, normalizedEmail, request.Auth0UserId, request.Connection), cancellationToken);
            }
        }

        // Step 4: Sync profile if changed (for existing users)
        if (!isNewUser && ShouldUpdateProfile(user, request))
        {
            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name != user.Name)
            {
                user.Name = request.Name;
                profileUpdated = true;
            }

            if (!string.IsNullOrWhiteSpace(request.AvatarUrl) && request.AvatarUrl != user.AvatarUrl)
            {
                user.AvatarUrl = request.AvatarUrl;
                profileUpdated = true;
            }

            if (profileUpdated)
            {
                user.Auth0LastSyncedAt = DateTime.UtcNow;
                user.UpdatedAt = DateTime.UtcNow;

                await eventPublisher.PublishAsync(new UserProfileSyncedEvent(
                    user.Id, user.Name, user.AvatarUrl), cancellationToken);
            }
        }

        // Step 5: Sync roles if provided
        if (request.Roles.Count > 0)
        {
            rolesUpdated = await SyncRolesAsync(
                user.Id, request.Roles, cancellationToken);
        }

        // Save changes
        if (wasLinked || profileUpdated || rolesUpdated)
        {
            await userRepository.UpdateAsync(user);
        }

        return new Auth0SyncResult(
            user.Id,
            isNewUser,
            wasLinked,
            profileUpdated,
            rolesUpdated);
    }

    private static bool ShouldUpdateProfile(UserEntity user, SyncAuth0UserCommand request)
    {
        return (!string.IsNullOrWhiteSpace(request.Name) && request.Name != user.Name)
            || (!string.IsNullOrWhiteSpace(request.AvatarUrl) && request.AvatarUrl != user.AvatarUrl);
    }

    private async Task<bool> SyncRolesAsync(
        Guid userId,
        IReadOnlyList<string> auth0Roles,
        CancellationToken cancellationToken)
    {
        var currentRoles = await roleRepository.GetRolesForUserAsync(userId, cancellationToken);
        var currentRoleNames = currentRoles.Select(r => r.Name).ToHashSet();
        var auth0RoleNames = auth0Roles.ToHashSet();

        // Skip if no changes
        if (currentRoleNames.SetEquals(auth0RoleNames))
            return false;

        // Remove roles not in Auth0
        foreach (var roleName in currentRoleNames.Except(auth0RoleNames))
        {
            var role = currentRoles.First(r => r.Name == roleName);
            await roleRepository.RemoveRoleFromUserAsync(userId, role.Id);
        }

        // Add roles from Auth0
        foreach (var roleName in auth0RoleNames.Except(currentRoleNames))
        {
            var role = await roleRepository.GetByNameAsync(roleName);
            if (role != null)
            {
                await roleRepository.AssignRoleToUserAsync(userId, role.Id);
            }
            else
            {
                logger.LogWarning("Auth0 role {RoleName} not found locally, skipping", roleName);
            }
        }

        await eventPublisher.PublishAsync(new UserRolesUpdatedEvent(userId, auth0Roles), cancellationToken);
        return true;
    }
}
