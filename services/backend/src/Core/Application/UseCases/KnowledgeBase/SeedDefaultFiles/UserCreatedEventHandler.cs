using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.KnowledgeBase.SeedDefaultFiles;

public class UserCreatedEventHandler(
    IUserCreditsRepository userCreditsRepository,
    IEventPublisher eventPublisher,
    IAgentRepository agentRepository,
    IUserPluginRepository userPluginRepository,
    ILogger<UserCreatedEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>
{
    private const int WELCOME_CREDITS = 10;

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing new user {UserId}", notification.UserId);

            // Grant welcome credits
            await GrantWelcomeCredits(notification.UserId, cancellationToken);

            // Assign plugins from public agents so user can use them immediately
            await AssignPublicAgentPluginsAsync(notification.UserId, cancellationToken);

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

            // Check if user already has credits
            var existingCredits = await userCreditsRepository.GetByUserIdAsync(userId, cancellationToken);
            if (existingCredits != null)
            {
                logger.LogWarning("User {UserId} already has credits record, skipping welcome credits", userId);
                return;
            }

            // Create a new UserCreditsEntity with welcome credits
            var userCredits = new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                AvailableCredits = WELCOME_CREDITS,
                TotalCreditsGranted = WELCOME_CREDITS,
                TotalCreditsConsumed = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await userCreditsRepository.CreateAsync(userCredits, cancellationToken);

            // Publish credits added event
            await eventPublisher.PublishAsync(new CreditsAddedEvent(userCredits.Id, userCredits.UserId, WELCOME_CREDITS));

            logger.LogInformation("Successfully granted {Credits} welcome credits to user {UserId}", WELCOME_CREDITS, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to grant welcome credits to user {UserId}", userId);
            // Don't throw - we don't want to fail user registration if credit granting fails
        }
    }

    private async Task AssignPublicAgentPluginsAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Assigning public agent plugins to user {UserId}", userId);

            // Get all agents and filter for public ones
            var allAgents = await agentRepository.ListAsync(cancellationToken);
            var publicAgents = allAgents.Where(a => a.IsPublic && !a.IsDeleted).ToList();

            // Collect unique plugin IDs from public agents
            var pluginIds = publicAgents
                .Where(a => a.PluginIds != null && a.PluginIds.Length > 0)
                .SelectMany(a => a.PluginIds)
                .Distinct()
                .ToList();

            if (pluginIds.Count == 0)
            {
                logger.LogInformation("No plugins to assign for user {UserId}", userId);
                return;
            }

            // Assign each plugin to the user
            foreach (var pluginId in pluginIds)
            {
                // Check if user already has this plugin
                var existingUserPlugin = await userPluginRepository.GetByUserAndPluginAsync(
                    userId, pluginId, cancellationToken);

                if (existingUserPlugin != null)
                {
                    logger.LogDebug("User {UserId} already has plugin {PluginId}", userId, pluginId);
                    continue;
                }

                var userPlugin = new UserPluginEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    PluginId = pluginId,
                    PurchasedAt = DateTime.UtcNow,
                    IsActive = true,
                    ExpiresAt = null // No expiration for default plugins
                };

                await userPluginRepository.CreateAsync(userPlugin, cancellationToken);
                logger.LogDebug("Assigned plugin {PluginId} to user {UserId}", pluginId, userId);
            }

            logger.LogInformation("Successfully assigned {Count} plugins to user {UserId}", pluginIds.Count, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to assign public agent plugins to user {UserId}", userId);
            // Don't throw - we don't want to fail user registration if plugin assignment fails
        }
    }
}
