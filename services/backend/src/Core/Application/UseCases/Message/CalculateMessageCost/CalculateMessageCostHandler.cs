using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Message.CalculateMessageCost;

public sealed class CalculateMessageCostHandler(
    IUnitOfWork unitOfWork,
    ICreditTrackingService creditTrackingService,
    INotificationService notificationService,
    ILogger<CalculateMessageCostHandler> logger
) : IRequestHandler<CalculateMessageCostCommand, CalculateMessageCostResult>
{
    private const int DEFAULT_TOKENS_PER_MESSAGE = 100; // Default estimate if not provided
    private const decimal DEFAULT_TOKEN_RATE = 0.001m; // Default credits per token
    private const int DEFAULT_FIXED_RATE = 5; // Default credits per message

    public async Task<CalculateMessageCostResult> Handle(CalculateMessageCostCommand command, CancellationToken cancellationToken)
    {
        const int MAX_RETRY_ATTEMPTS = 3;
        int retryCount = 0;
        
        while (retryCount < MAX_RETRY_ATTEMPTS)
        {
            try
            {
                await unitOfWork.BeginTransactionAsync(cancellationToken);
                
                // Get user's billing preferences
                var billingPreference = await unitOfWork.UserBillingPreferences.GetByUserIdAsync(command.UserId, cancellationToken);
            
            // If no preference exists, create default
            if (billingPreference == null)
            {
                billingPreference = new UserBillingPreferenceEntity
                {
                    Id = Guid.NewGuid(),
                    UserId = command.UserId,
                    MessageBillingMethod = "tokens",
                    TokenRate = DEFAULT_TOKEN_RATE,
                    FixedMessageRate = DEFAULT_FIXED_RATE,
                    LowCreditThreshold = 100,
                    EnableLowCreditAlerts = true
                };
                await unitOfWork.UserBillingPreferences.CreateAsync(billingPreference, cancellationToken);
            }

            // Calculate message cost based on billing method
            int messageCredits = 0;
            int? tokensUsed = null;
            decimal? costPerToken = null;
            decimal? fixedRate = null;

            if (billingPreference.MessageBillingMethod == "tokens")
            {
                // Token-based billing
                tokensUsed = command.TokenCount ?? EstimateTokenCount(command.MessageContent);
                costPerToken = billingPreference.TokenRate;
                messageCredits = (int)Math.Ceiling(tokensUsed.Value * costPerToken.Value);
            }
            else
            {
                // Fixed-rate billing
                fixedRate = billingPreference.FixedMessageRate;
                messageCredits = billingPreference.FixedMessageRate;
            }

            // Calculate total credits including additional costs
            int additionalCredits = command.AdditionalPluginCredits + command.AdditionalWorkflowCredits;
            int totalCredits = messageCredits + additionalCredits;

            // Validate user has sufficient credits
            var subscription = await unitOfWork.Subscriptions.GetByUserIdAsync(command.UserId, cancellationToken);
            if (subscription == null)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new CalculateMessageCostResult(false, Error: "No active subscription found");
            }
            
            // Store the version before modification
            var expectedVersion = subscription.Version;

            if (!subscription.HasUnlimitedCredits() && subscription.Credits < totalCredits)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new CalculateMessageCostResult(false, 
                    Error: $"Insufficient credits. Required: {totalCredits}, Available: {subscription.Credits}");
            }

            // Record message cost
            var messageCost = new MessageCostEntity
            {
                Id = Guid.NewGuid(),
                MessageId = command.MessageId,
                UserId = command.UserId,
                BillingMethod = billingPreference.MessageBillingMethod,
                TokensConsumed = tokensUsed,
                CostPerToken = costPerToken,
                FixedRate = fixedRate,
                TotalCredits = messageCredits,
                HasPluginUsage = command.HasPluginUsage,
                HasWorkflowUsage = command.HasWorkflowUsage,
                AdditionalCredits = additionalCredits,
                CreatedAt = DateTime.UtcNow
            };

            await unitOfWork.MessageCosts.CreateAsync(messageCost, cancellationToken);

            // Deduct credits from subscription
            subscription.ConsumeCredits(totalCredits);
            await unitOfWork.Subscriptions.UpdateWithVersionCheckAsync(subscription, expectedVersion, cancellationToken);
            
            // Save all changes within the transaction
            await unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Commit transaction
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            // Track credit consumption
            await creditTrackingService.TrackMessageCostAsync(
                command.UserId,
                command.MessageId,
                $"Message ({billingPreference.MessageBillingMethod} billing)",
                totalCredits,
                cancellationToken);

            // Check if low credit alert should be triggered
            if (billingPreference.EnableLowCreditAlerts && 
                billingPreference.LowCreditThreshold.HasValue &&
                subscription.Credits <= billingPreference.LowCreditThreshold.Value)
            {
                logger.LogWarning("User {UserId} has low credits: {Credits} remaining", 
                    command.UserId, subscription.Credits);
                
                // Send low credit alert
                await notificationService.SendLowCreditAlertAsync(
                    command.UserId, 
                    subscription.Credits, 
                    billingPreference.LowCreditThreshold.Value,
                    cancellationToken);
            }
            
            // Check if credits are exhausted
            if (subscription.Credits == 0)
            {
                await notificationService.SendCreditExhaustedAlertAsync(command.UserId, cancellationToken);
            }

            logger.LogInformation("Message cost calculated for user {UserId}: {TotalCredits} credits ({MessageCredits} + {AdditionalCredits})",
                command.UserId, totalCredits, messageCredits, additionalCredits);

            return new CalculateMessageCostResult(
                true,
                TotalCredits: totalCredits,
                MessageCredits: messageCredits,
                AdditionalCredits: additionalCredits,
                BillingMethod: billingPreference.MessageBillingMethod
            );
            }
            catch (ConcurrencyException ex)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                retryCount++;
                
                if (retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    logger.LogError(ex, "Max retry attempts reached for message cost calculation for user {UserId}", command.UserId);
                    return new CalculateMessageCostResult(false, Error: "Failed to process message due to high system load. Please try again.");
                }
                
                logger.LogWarning("Concurrency conflict detected in message cost calculation, retrying... Attempt {RetryCount} of {MaxAttempts}", 
                    retryCount, MAX_RETRY_ATTEMPTS);
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount - 1)), cancellationToken);
            }
            catch (Exception ex)
            {
                await unitOfWork.RollbackTransactionAsync(cancellationToken);
                logger.LogError(ex, "Error calculating message cost for user {UserId}", command.UserId);
                return new CalculateMessageCostResult(false, Error: "An error occurred while calculating message cost");
            }
        }
        
        return new CalculateMessageCostResult(false, Error: "Failed to calculate message cost after maximum retry attempts");
    }

    private int EstimateTokenCount(string content)
    {
        // Simple estimation: ~4 characters per token (rough approximation)
        // In production, you'd use the actual tokenizer for the model being used
        return Math.Max(1, content.Length / 4);
    }
}