using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.KnowledgeBase.SeedDefaultFiles;

public class UserCreatedEventHandler(
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher,
    ILogger<UserCreatedEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>
{
    private const int WELCOME_CREDITS = 1000;

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing new user {UserId}", notification.UserId);

            // Grant welcome credits
            await GrantWelcomeCredits(notification.UserId, cancellationToken);

            // Note: Knowledge base seeding from local files is disabled for cloud deployment.
            // Users can upload their own files via the /knowledge-base API endpoint.
            logger.LogInformation("User {UserId} setup complete. Users can upload KB files via API.", notification.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process new user {UserId}", notification.UserId);
            // Don't throw - we don't want to fail user registration
        }
    }

    private async Task GrantWelcomeCredits(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Granting {Credits} welcome credits to user {UserId}", WELCOME_CREDITS, userId);
            
            // Check if user already has a subscription
            var existingSubscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existingSubscription != null)
            {
                logger.LogWarning("User {UserId} already has a subscription, skipping welcome credits", userId);
                return;
            }
            
            // Create a new subscription with welcome credits
            var subscription = new SubscriptionEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Credits = WELCOME_CREDITS,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpiresAt = null, // No expiration for welcome credits
                AutoRenew = false
            };
            
            await subscriptionRepository.CreateAsync(subscription, cancellationToken);
            
            // Publish subscription created event
            await eventPublisher.PublishAsync(new SubscriptionCreatedEvent(subscription.Id, subscription.UserId));
            
            logger.LogInformation("Successfully granted {Credits} welcome credits to user {UserId}", WELCOME_CREDITS, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to grant welcome credits to user {UserId}", userId);
            // Don't throw - we don't want to fail user registration if credit granting fails
        }
    }
}