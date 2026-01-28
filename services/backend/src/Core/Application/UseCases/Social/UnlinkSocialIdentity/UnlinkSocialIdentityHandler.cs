using Application.Interfaces;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Social.UnlinkSocialIdentity;

/// <summary>
/// Handler for unlinking a social identity from a user account.
/// </summary>
public class UnlinkSocialIdentityHandler(
    ISocialIdentityRepository socialIdentityRepository,
    IUserRepository userRepository,
    IEventPublisher eventPublisher,
    ILogger<UnlinkSocialIdentityHandler> logger)
    : IRequestHandler<UnlinkSocialIdentityCommand, Unit>
{
    public async Task<Unit> Handle(
        UnlinkSocialIdentityCommand request,
        CancellationToken cancellationToken)
    {
        // Find the identity to unlink
        var identity = await socialIdentityRepository.GetByProviderAsync(
            request.UserId, request.Provider, cancellationToken);

        if (identity == null || identity.UnlinkedAt != null)
        {
            throw new SocialIdentityNotFoundException(request.Provider, request.UserId);
        }

        // Get all active identities for this user
        var allIdentities = await socialIdentityRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        var activeIdentities = allIdentities.Where(i => i.UnlinkedAt == null).ToList();

        // Get user to check if they have a password
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        // Cannot unlink if this is the only identity and user has no password
        if (activeIdentities.Count == 1 && string.IsNullOrEmpty(user?.PasswordHash))
        {
            throw new ValidationException(
                "Cannot unlink the only social identity without a password set. " +
                "Please set a password first or link another identity.");
        }

        // Soft delete the identity
        identity.UnlinkedAt = DateTime.UtcNow;
        await socialIdentityRepository.UpdateAsync(identity, cancellationToken);

        logger.LogInformation(
            "Social identity unlinked. UserId: {UserId}, Provider: {Provider}, IdentityId: {IdentityId}",
            request.UserId, request.Provider, identity.Id);

        // Publish event
        await eventPublisher.PublishAsync(new SocialIdentityUnlinkedEvent(
            request.UserId, identity.Id, request.Provider), cancellationToken);

        return Unit.Value;
    }
}
