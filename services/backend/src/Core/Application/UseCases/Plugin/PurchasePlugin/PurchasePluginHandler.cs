using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.UseCases.Plugin.PurchasePlugin;

public sealed class PurchasePluginHandler(
    IUnitOfWork unitOfWork,
    ICreditTrackingService creditTrackingService,
    ICostRegistry costRegistry,
    ICreditLimitService creditLimitService,
    ILogger<PurchasePluginHandler> logger
) : IRequestHandler<PurchasePluginCommand, PurchasePluginResult>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICreditTrackingService _creditTracking = creditTrackingService;
    private readonly ICostRegistry _costRegistry = costRegistry;
    private readonly ICreditLimitService _creditLimitService = creditLimitService;
    private readonly ILogger<PurchasePluginHandler> _logger = logger;

    public async Task<PurchasePluginResult> Handle(PurchasePluginCommand command, CancellationToken cancellationToken)
    {
        const int MAX_RETRY_ATTEMPTS = 3;
        int retryCount = 0;
        
        while (retryCount < MAX_RETRY_ATTEMPTS)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                
                // Check if user already owns the plugin
                var existingPurchase = await _unitOfWork.UserPlugins.GetByUserAndPluginAsync(
                    command.UserId, command.PluginId, cancellationToken);
                
                if (existingPurchase != null && existingPurchase.IsActive)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return new PurchasePluginResult(false, "You already own this plugin");
                }

                // Get the plugin details
                var plugin = await _unitOfWork.Plugins.GetByIdAsync(command.PluginId, cancellationToken);
                if (plugin == null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return new PurchasePluginResult(false, "Plugin not found");
                }

                if (!plugin.IsActive)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return new PurchasePluginResult(false, "Plugin is not available for purchase");
                }

                // Get user subscription
                var subscription = await _unitOfWork.Subscriptions.GetByUserIdAsync(command.UserId, cancellationToken);
                if (subscription == null)
                {
                    await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                    return new PurchasePluginResult(false, "No active subscription found");
                }
                
                // Store the version before modification
                var expectedVersion = subscription.Version;

                // Check if plugin requires subscription
                if (plugin.IsSubscriptionBased)
                {
                    // For subscription-based plugins, just verify user has active subscription
                    if (subscription.ExpiresAt.HasValue && subscription.ExpiresAt.Value < DateTime.UtcNow)
                    {
                        await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                        return new PurchasePluginResult(false, "Your subscription has expired");
                    }
                }
                else
                {
                    // Get the actual cost from the registry
                    var purchaseCost = await _costRegistry.GetPluginPurchaseCostAsync(plugin.Id, cancellationToken);

                    if (!subscription.HasUnlimitedCredits())
                    {
                        // Check credit limits
                        var limitCheck = await _creditLimitService.CheckLimitsAsync(
                            command.UserId,
                            purchaseCost,
                            "plugin_purchase",
                            cancellationToken);

                        if (!limitCheck.IsWithinLimits)
                        {
                            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                            return new PurchasePluginResult(false, "Credit limit exceeded");
                        }

                        if (subscription.Credits < purchaseCost)
                        {
                            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                            return new PurchasePluginResult(false,
                                $"Insufficient credits. You need {purchaseCost} credits but only have {subscription.Credits}");
                        }
                    }

                    // Consume credits
                    subscription.ConsumeCredits(purchaseCost);
                    
                    // Update with version check
                    await _unitOfWork.Subscriptions.UpdateWithVersionCheckAsync(
                        subscription, expectedVersion, cancellationToken);

                    // Update credit limits INSIDE the transaction
                    if (!subscription.HasUnlimitedCredits())
                    {
                        await _creditLimitService.ConsumeLimitsAsync(
                            command.UserId,
                            purchaseCost,
                            "plugin_purchase",
                            cancellationToken);
                    }
                }

                // Create or update user plugin record
                if (existingPurchase != null)
                {
                    existingPurchase.Reactivate();
                    existingPurchase.PurchasedAt = DateTime.UtcNow;
                    if (plugin.IsSubscriptionBased)
                    {
                        existingPurchase.ExpiresAt = subscription.ExpiresAt;
                    }
                    await _unitOfWork.UserPlugins.UpdateAsync(existingPurchase, cancellationToken);
                }
                else
                {
                    var userPlugin = new UserPluginEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = command.UserId,
                        PluginId = command.PluginId,
                        PurchasedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    if (plugin.IsSubscriptionBased)
                    {
                        userPlugin.ExpiresAt = subscription.ExpiresAt;
                    }

                    await _unitOfWork.UserPlugins.CreateAsync(userPlugin, cancellationToken);
                }
                
                // Save all changes within the transaction
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                // Commit transaction
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                
                // Track credit consumption after successful transaction
                if (!plugin.IsSubscriptionBased && !subscription.HasUnlimitedCredits())
                {
                    var purchaseCost = await _costRegistry.GetPluginPurchaseCostAsync(plugin.Id, cancellationToken);
                    await _creditTracking.TrackPluginUsageAsync(
                        command.UserId,
                        plugin.Id,
                        plugin.Name,
                        purchaseCost,
                        JsonSerializer.Serialize(new { action = "purchase", pluginId = plugin.Id, pluginName = plugin.Name }),
                        cancellationToken);
                }

                _logger.LogInformation("User {UserId} successfully purchased plugin {PluginId} ({PluginName})", 
                    command.UserId, plugin.Id, plugin.Name);

                return new PurchasePluginResult(true);
            }
            catch (ConcurrencyException ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                retryCount++;
                
                if (retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogError(ex, "Max retry attempts reached for purchasing plugin {PluginId} for user {UserId}", 
                        command.PluginId, command.UserId);
                    return new PurchasePluginResult(false, "Unable to complete plugin purchase due to concurrent updates. Please try again.");
                }
                
                _logger.LogWarning("Concurrency conflict detected, retrying... Attempt {RetryCount} of {MaxAttempts}", 
                    retryCount, MAX_RETRY_ATTEMPTS);
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount - 1)), cancellationToken);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogError(ex, "Error purchasing plugin {PluginId} for user {UserId}", 
                    command.PluginId, command.UserId);
                return new PurchasePluginResult(false, "An error occurred while processing your purchase");
            }
        }
        
        return new PurchasePluginResult(false, "Failed to purchase plugin after maximum retry attempts");
    }
}

// Extension method for UserPluginEntity
public static class UserPluginEntityExtensions
{
    public static void Reactivate(this UserPluginEntity entity)
    {
        entity.IsActive = true;
    }
}