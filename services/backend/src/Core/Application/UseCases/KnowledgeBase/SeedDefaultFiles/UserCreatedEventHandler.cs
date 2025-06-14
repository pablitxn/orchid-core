using Application.Interfaces;
using Domain.Entities;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.KnowledgeBase.SeedDefaultFiles;

public class UserCreatedEventHandler(
    IKnowledgeBaseFileRepository knowledgeBaseRepository,
    IFileStorageService fileStorage,
    ISubscriptionRepository subscriptionRepository,
    IEventPublisher eventPublisher,
    ILogger<UserCreatedEventHandler> logger)
    : INotificationHandler<UserCreatedEvent>
{
    private readonly string _defaultDocumentsPath = "data/knowledge_base/default_documents";
    private const int WELCOME_CREDITS = 1000;

    public async Task Handle(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Processing new user {UserId}", notification.UserId);
            
            // Grant welcome credits first
            await GrantWelcomeCredits(notification.UserId, cancellationToken);
            
            // Then seed knowledge base files
            logger.LogInformation("Seeding default knowledge base files for user {UserId}", notification.UserId);

            // Check if user already has files
            var existingFiles = await knowledgeBaseRepository.GetPaginatedAsync(
                Guid.NewGuid()
            );

            if (existingFiles.totalCount > 0)
                return;

            var defaultFiles = new[]
            {
                new
                {
                    FileName = "manifest.txt",
                    Title = "System Manifest",
                    Description = "Core principles and guidelines for using the knowledge base system",
                    Tags = new[] { "system", "guidelines", "manifest", "documentation" },
                    MimeType = "application/pdf"
                },
                new
                {
                    FileName = "instructions.md",
                    Title = "Knowledge Base Instructions",
                    Description = "Comprehensive guide on how to use the knowledge base effectively",
                    Tags = new[] { "instructions", "guide", "help", "documentation" },
                    MimeType = "text/markdown"
                },
                new
                {
                    FileName = "system_prompt.md",
                    Title = "AI System Prompt",
                    Description = "Configuration and behavioral guidelines for the AI assistant",
                    Tags = new[] { "ai", "system", "prompt", "configuration" },
                    MimeType = "text/markdown"
                },
                new
                {
                    FileName = "products.csv",
                    Title = "Product Catalog",
                    Description = "Sample product catalog with pricing and inventory information",
                    Tags = new[] { "products", "catalog", "inventory", "sample-data" },
                    MimeType = "text/csv"
                },
                new
                {
                    FileName = "trial_balance.csv",
                    Title = "Financial Trial Balance",
                    Description = "Sample financial trial balance showing accounts and balances",
                    Tags = new[] { "finance", "accounting", "trial-balance", "sample-data" },
                    MimeType = "text/csv"
                },
                new
                {
                    FileName = "budget_2025.csv",
                    Title = "Budget Planning 2025",
                    Description = "Quarterly budget planning spreadsheet with revenue and expense projections",
                    Tags = new[] { "budget", "planning", "finance", "projections", "sample-data" },
                    MimeType = "text/csv"
                }
            };

            foreach (var file in defaultFiles)
            {
                try
                {
                    var filePath = Path.Combine(_defaultDocumentsPath, file.FileName);

                    if (!File.Exists(filePath))
                    {
                        logger.LogWarning("Default file not found: {FilePath}", filePath);
                        continue;
                    }

                    var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                    var fileSize = fileBytes.Length;

                    // Generate unique file name for storage
                    var uniqueFileName = $"kb/{notification.UserId}/{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";

                    // Upload file to storage
                    var fileUrl = await fileStorage.StoreFileAsync(
                        new MemoryStream(fileBytes),
                        uniqueFileName,
                        file.MimeType
                    );

                    // Create knowledge base entry
                    var kbFile = new KnowledgeBaseFileEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = notification.UserId,
                        Title = file.Title,
                        Description = file.Description,
                        Tags = file.Tags,
                        MimeType = file.MimeType,
                        FileUrl = fileUrl,
                        FileSize = fileSize
                    };

                    await knowledgeBaseRepository.AddAsync(kbFile, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error seeding file {FileName} for user {UserId}", file.FileName,
                        notification.UserId);
                }
            }

            logger.LogInformation("Successfully seeded knowledge base files for user {UserId}", notification.UserId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed knowledge base files for user {UserId}", notification.UserId);
            // Don't throw - we don't want to fail user registration if KB seeding fails
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