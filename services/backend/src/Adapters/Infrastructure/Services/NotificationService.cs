using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Infrastructure.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    IUserRepository userRepository,
    IRealtimeNotificationPort realtimeNotificationPort,
    INotificationPreferencesRepository preferencesRepository,
    IMatrixBridgePort matrixBridgePort,
    ILogger<NotificationService> logger)
    : INotificationService
{
    public async Task SendLowCreditAlertAsync(Guid userId, int remainingCredits, int threshold,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.LowCreditAlert.ToString(),
            Title = "Low Credit Balance Alert",
            Message =
                $"Your credit balance is running low. You have {remainingCredits} credits remaining, which is below your threshold of {threshold} credits.",
            ActionUrl = "/credits",
            ActionText = "Add Credits",
            Priority = 2,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Metadata = JsonSerializer.Serialize(new { remainingCredits, threshold })
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogInformation(
            "Low credit alert sent to user {UserId}. Remaining: {RemainingCredits}, Threshold: {Threshold}",
            userId, remainingCredits, threshold);
    }

    public async Task SendCreditExhaustedAlertAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.CreditExhausted.ToString(),
            Title = "Credits Exhausted",
            Message = "You have run out of credits. Please add more credits to continue using our services.",
            ActionUrl = "/credits",
            ActionText = "Add Credits Now",
            Priority = 3,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogWarning("Credit exhausted alert sent to user {UserId}", userId);
    }

    public async Task SendCreditAddedNotificationAsync(Guid userId, int creditsAdded, int newBalance,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.CreditAdded.ToString(),
            Title = "Credits Added Successfully",
            Message =
                $"Successfully added {creditsAdded} credits to your account. Your new balance is {newBalance} credits.",
            Priority = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Metadata = JsonSerializer.Serialize(new { creditsAdded, newBalance })
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogInformation(
            "Credit added notification sent to user {UserId}. Added: {CreditsAdded}, New Balance: {NewBalance}",
            userId, creditsAdded, newBalance);
    }

    public async Task SendPurchaseConfirmationAsync(Guid userId, string itemType, string itemName, int creditsCost,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.PurchaseConfirmation.ToString(),
            Title = $"{itemType} Purchase Confirmed",
            Message = $"You have successfully purchased {itemName} for {creditsCost} credits.",
            ActionUrl = itemType.ToLower() == "plugin" ? "/plugins" : "/workflows",
            ActionText = $"View Your {itemType}s",
            Priority = 1,
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            Metadata = JsonSerializer.Serialize(new { itemType, itemName, creditsCost })
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogInformation(
            "Purchase confirmation sent to user {UserId}. Type: {ItemType}, Name: {ItemName}, Cost: {CreditsCost}",
            userId, itemType, itemName, creditsCost);
    }

    public async Task SendSubscriptionExpiringAlertAsync(Guid userId, DateTime expirationDate,
        CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var daysRemaining = (expirationDate - DateTime.UtcNow).Days;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.SubscriptionExpiring.ToString(),
            Title = "Subscription Expiring Soon",
            Message =
                $"Your subscription will expire in {daysRemaining} days on {expirationDate:MMM dd, yyyy}. Renew now to avoid service interruption.",
            ActionUrl = "/subscription",
            ActionText = "Renew Subscription",
            Priority = 2,
            ExpiresAt = expirationDate,
            Metadata = JsonSerializer.Serialize(new { expirationDate, daysRemaining })
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogInformation(
            "Subscription expiring alert sent to user {UserId}. Expires: {ExpirationDate}, Days Remaining: {DaysRemaining}",
            userId, expirationDate, daysRemaining);
    }

    public async Task SendSubscriptionExpiredAlertAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return;

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.SubscriptionExpired.ToString(),
            Title = "Subscription Expired",
            Message =
                "Your subscription has expired. Please renew your subscription to continue using premium features.",
            ActionUrl = "/subscription",
            ActionText = "Renew Now",
            Priority = 3,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        logger.LogWarning("Subscription expired alert sent to user {UserId}", userId);
    }

    private async Task SendRealTimeNotification(Guid userId, NotificationEntity notification)
    {
        await realtimeNotificationPort.SendNotificationAsync(userId, new
        {
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.ActionUrl,
            notification.ActionText,
            notification.Priority,
            notification.CreatedAt
        });
    }

    public async Task SendNotificationAsync(
        Guid userId,
        string type,
        string title,
        string message,
        int priority = 1,
        string? actionUrl = null,
        string? actionText = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var prefs = await preferencesRepository.GetOrCreateDefaultAsync(userId, cancellationToken);

        if (!prefs.NotificationsEnabled)
        {
            logger.LogDebug("Notifications disabled for user {UserId}", userId);
            return;
        }

        if (!IsTypeEnabled(prefs, type))
        {
            logger.LogDebug("Notification type {Type} disabled for user {UserId}", type, userId);
            return;
        }

        var notification = new NotificationEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            ActionUrl = actionUrl,
            ActionText = actionText,
            Priority = priority,
            Metadata = metadata,
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };

        await notificationRepository.CreateAsync(notification, cancellationToken);
        await SendRealTimeNotification(userId, notification);

        if (prefs.MatrixNotificationsEnabled)
        {
            await matrixBridgePort.SendNotificationAsync(userId, title, message, type, metadata, cancellationToken);
        }

        logger.LogInformation("Notification sent to user {UserId}, type: {Type}", userId, type);
    }

    public async Task SendReminderAsync(
        Guid userId,
        string title,
        string message,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await SendNotificationAsync(
            userId,
            NotificationType.Reminder.ToString(),
            title,
            message,
            priority: 2,
            metadata: metadata,
            cancellationToken: cancellationToken);
    }

    public async Task SendInsightAsync(
        Guid userId,
        string title,
        string message,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        await SendNotificationAsync(
            userId,
            NotificationType.Insight.ToString(),
            title,
            message,
            priority: 1,
            metadata: metadata,
            cancellationToken: cancellationToken);
    }

    public async Task SendIngestionConfirmationAsync(
        Guid userId,
        string itemType,
        string itemName,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        var tagString = tags != null && tags.Length > 0 ? $" [tags: {string.Join(", ", tags)}]" : "";
        await SendNotificationAsync(
            userId,
            NotificationType.IngestionConfirmation.ToString(),
            "Content Saved",
            $"Saved {itemType} as reference: {itemName}{tagString}",
            priority: 0,
            metadata: JsonSerializer.Serialize(new { itemType, itemName, tags }),
            cancellationToken: cancellationToken);
    }

    public async Task SendSyncAlertAsync(
        Guid userId,
        string serviceName,
        bool success,
        string message,
        CancellationToken cancellationToken = default)
    {
        await SendNotificationAsync(
            userId,
            NotificationType.SyncAlert.ToString(),
            success ? "Sync Completed" : "Sync Issue",
            message,
            priority: success ? 0 : 2,
            metadata: JsonSerializer.Serialize(new { serviceName, success }),
            cancellationToken: cancellationToken);
    }

    private static bool IsTypeEnabled(NotificationPreferencesEntity prefs, string type)
    {
        string[] enabledTypes;
        try
        {
            enabledTypes = JsonSerializer.Deserialize<string[]>(prefs.EnabledTypes) ?? [];
        }
        catch
        {
            enabledTypes = ["reminders", "insights", "ingestion", "sync"];
        }

        var typeCategory = type.ToLowerInvariant() switch
        {
            "reminder" or "deadlineapproaching" => "reminders",
            "insight" or "weeklyperformancesummary" or "trendalert" => "insights",
            "ingestionconfirmation" => "ingestion",
            "syncalert" => "sync",
            _ => "general"
        };

        return enabledTypes.Contains(typeCategory) || enabledTypes.Contains("all") || typeCategory == "general";
    }
}