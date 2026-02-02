using Application.Interfaces;
using Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Handles credit-related events and sends real-time notifications
/// </summary>
public class CreditNotificationHandler(
    ICreditNotificationPort creditNotificationPort,
    IUserCreditsRepository userCreditsRepository,
    ISubscriptionRepository subscriptionRepository,
    ILogger<CreditNotificationHandler> logger)
    :
        IConsumer<CreditsConsumedEvent>,
        IConsumer<CreditsAddedEvent>
{
    private const int LOW_CREDIT_THRESHOLD = 100;

    public async Task Consume(ConsumeContext<CreditsConsumedEvent> context)
    {
        var notification = context.Message;
        var cancellationToken = context.CancellationToken;

        try
        {
            // Get user credits for current balance
            var userCredits = await userCreditsRepository.GetByUserIdAsync(notification.UserId, cancellationToken);
            if (userCredits == null) return;

            // Check if user has unlimited subscription
            var subscription = await subscriptionRepository.GetByUserIdAsync(notification.UserId, cancellationToken);
            var hasUnlimited = subscription?.HasUnlimitedCredits() ?? false;

            // Send real-time update
            await creditNotificationPort.NotifyCreditBalanceUpdatedAsync(
                notification.UserId,
                userCredits.AvailableCredits,
                hasUnlimited,
                cancellationToken);

            // Send consumption notification
            await creditNotificationPort.NotifyCreditConsumedAsync(
                notification.UserId,
                notification.Amount,
                notification.ResourceType ?? "general",
                notification.ResourceName ?? "System",
                cancellationToken);

            // Check for low credit warning
            if (!hasUnlimited && userCredits.AvailableCredits < LOW_CREDIT_THRESHOLD)
            {
                await creditNotificationPort.NotifyLowCreditWarningAsync(
                    notification.UserId,
                    userCredits.AvailableCredits,
                    LOW_CREDIT_THRESHOLD,
                    cancellationToken);

                logger.LogWarning("Low credit warning sent to user {UserId}. Balance: {Balance}",
                    notification.UserId, userCredits.AvailableCredits);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending credit consumption notification for user {UserId}",
                notification.UserId);
        }
    }

    public async Task Consume(ConsumeContext<CreditsAddedEvent> context)
    {
        var notification = context.Message;
        var cancellationToken = context.CancellationToken;

        try
        {
            // Get user credits for current balance
            var userCredits = await userCreditsRepository.GetByUserIdAsync(notification.UserId, cancellationToken);
            if (userCredits == null) return;

            // Check if user has unlimited subscription
            var subscription = await subscriptionRepository.GetByUserIdAsync(notification.UserId, cancellationToken);
            var hasUnlimited = subscription?.HasUnlimitedCredits() ?? false;

            // Send real-time update
            await creditNotificationPort.NotifyCreditsAddedAsync(
                notification.UserId,
                notification.Amount,
                userCredits.AvailableCredits,
                hasUnlimited,
                cancellationToken);

            logger.LogInformation("Credit balance update sent to user {UserId}. New balance: {Balance}",
                notification.UserId, userCredits.AvailableCredits);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending credit addition notification for user {UserId}",
                notification.UserId);
        }
    }
}
