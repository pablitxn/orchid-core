using Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using WebApi.Hubs;

namespace WebApi.Adapters;

/// <summary>
/// SignalR adapter implementation of ICreditNotificationPort
/// </summary>
public class SignalRCreditNotificationAdapter : ICreditNotificationPort
{
    private readonly IHubContext<CreditHub> _creditHub;
    private readonly ILogger<SignalRCreditNotificationAdapter> _logger;

    public SignalRCreditNotificationAdapter(
        IHubContext<CreditHub> creditHub,
        ILogger<SignalRCreditNotificationAdapter> logger)
    {
        _creditHub = creditHub;
        _logger = logger;
    }

    public async Task NotifyCreditBalanceUpdatedAsync(
        Guid userId, 
        int newBalance, 
        bool hasUnlimitedCredits,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _creditHub.Clients
                .Group($"user-{userId}")
                .SendAsync("CreditBalanceUpdated", new 
                { 
                    balance = newBalance, 
                    hasUnlimitedCredits,
                    timestamp = DateTime.UtcNow 
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send credit balance update to user {UserId}", userId);
        }
    }

    public async Task NotifyCreditConsumedAsync(
        Guid userId, 
        int amount, 
        string resourceType, 
        string resourceName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _creditHub.Clients
                .Group($"user-{userId}")
                .SendAsync("CreditConsumed", new 
                { 
                    amount, 
                    resourceType,
                    resourceName,
                    timestamp = DateTime.UtcNow 
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send credit consumed notification to user {UserId}", userId);
        }
    }

    public async Task NotifyLowCreditWarningAsync(
        Guid userId, 
        int currentBalance, 
        int threshold,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _creditHub.Clients
                .Group($"user-{userId}")
                .SendAsync("LowCreditWarning", new 
                { 
                    currentBalance, 
                    threshold,
                    timestamp = DateTime.UtcNow 
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send low credit warning to user {UserId}", userId);
        }
    }

    public async Task NotifyCreditsAddedAsync(
        Guid userId,
        int amount,
        int newBalance,
        bool hasUnlimitedCredits,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _creditHub.Clients
                .Group($"user-{userId}")
                .SendAsync("CreditsAdded", new 
                { 
                    amount,
                    balance = newBalance, 
                    hasUnlimitedCredits,
                    timestamp = DateTime.UtcNow 
                }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send credits added notification to user {UserId}", userId);
        }
    }
}