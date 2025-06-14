using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.UseCases.Plugin.ExecutePlugin;

public sealed class ExecutePluginHandler(
    IPluginRepository pluginRepository,
    IUserPluginRepository userPluginRepository,
    ISubscriptionRepository subscriptionRepository,
    ICreditTrackingService creditTrackingService,
    ICreditLimitService creditLimitService,
    IPluginDiscoveryService pluginDiscoveryService,
    ICostRegistry costRegistry,
    ILogger<ExecutePluginHandler> logger
) : IRequestHandler<ExecutePluginCommand, ExecutePluginResult>
{
    public async Task<ExecutePluginResult> Handle(ExecutePluginCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Verify user owns the plugin
            var userPlugin = await userPluginRepository.GetByUserAndPluginAsync(
                command.UserId, command.PluginId, cancellationToken);
            
            if (userPlugin == null || !userPlugin.IsActive)
            {
                return new ExecutePluginResult(false, Error: "You do not have access to this plugin");
            }

            // Check if plugin is subscription-based and verify subscription
            var plugin = await pluginRepository.GetByIdAsync(command.PluginId, cancellationToken);
            if (plugin == null)
            {
                return new ExecutePluginResult(false, Error: "Plugin not found");
            }

            var subscription = await subscriptionRepository.GetByUserIdAsync(command.UserId, cancellationToken);
            if (subscription == null)
            {
                return new ExecutePluginResult(false, Error: "No active subscription found");
            }

            // For subscription-based plugins, verify subscription is active
            if (plugin.IsSubscriptionBased)
            {
                if (subscription.ExpiresAt.HasValue && subscription.ExpiresAt.Value < DateTime.UtcNow)
                {
                    return new ExecutePluginResult(false, Error: "Your subscription has expired");
                }
            }

            // Calculate credits needed for this execution (could be based on plugin config or usage)
            int creditsRequired = await CalculateCreditsForExecution(plugin, command.Parameters, cancellationToken);
            
            // Validate sufficient credits
            var hasCredits = await creditTrackingService.ValidateSufficientCreditsAsync(
                command.UserId, creditsRequired, cancellationToken);

            if (!hasCredits)
            {
                return new ExecutePluginResult(false,
                    Error: $"Insufficient credits. This operation requires {creditsRequired} credits");
            }

            if (!subscription.HasUnlimitedCredits())
            {
                var limitCheck = await creditLimitService.CheckLimitsAsync(
                    command.UserId,
                    creditsRequired,
                    "plugin_usage",
                    cancellationToken);

                if (!limitCheck.IsWithinLimits)
                {
                    return new ExecutePluginResult(false, Error: "Credit limit exceeded");
                }
            }

            // Execute the plugin via discovery service
            // This is a placeholder - actual implementation would integrate with Semantic Kernel
            var result = await ExecutePluginViaSemanticKernel(plugin, command.Parameters, cancellationToken);

            // Deduct credits
            subscription.ConsumeCredits(creditsRequired);
            await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

            if (!subscription.HasUnlimitedCredits())
            {
                await creditLimitService.ConsumeLimitsAsync(
                    command.UserId,
                    creditsRequired,
                    "plugin_usage",
                    cancellationToken);
            }

            // Track credit consumption
            await creditTrackingService.TrackPluginUsageAsync(
                command.UserId,
                plugin.Id,
                plugin.Name,
                creditsRequired,
                JsonSerializer.Serialize(new { action = "execute", parameters = command.Parameters }),
                cancellationToken);

            logger.LogInformation("Plugin {PluginName} executed successfully for user {UserId}, consumed {Credits} credits",
                plugin.Name, command.UserId, creditsRequired);

            return new ExecutePluginResult(true, Result: result, CreditsUsed: creditsRequired);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing plugin {PluginId} for user {UserId}",
                command.PluginId, command.UserId);
            return new ExecutePluginResult(false, Error: "An error occurred while executing the plugin");
        }
    }

    private async Task<int> CalculateCreditsForExecution(Domain.Entities.PluginEntity plugin, string parameters, CancellationToken cancellationToken)
    {
        // Get the configured cost for this plugin
        var baseCost = await costRegistry.GetPluginUsageCostAsync(plugin.Id, cancellationToken);
        
        // Additional cost modifiers based on parameters
        var parameterModifier = 1.0m;
        
        try
        {
            var paramsJson = JsonDocument.Parse(parameters);
            
            // Example: If processing multiple items, multiply cost
            if (paramsJson.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                parameterModifier = Math.Max(1, items.GetArrayLength() / 10.0m); // 10% increase per 10 items
            }
            
            // Example: If high quality/precision requested
            if (paramsJson.RootElement.TryGetProperty("quality", out var quality) && 
                quality.GetString()?.ToLower() == "high")
            {
                parameterModifier *= 1.5m; // 50% increase for high quality
            }
        }
        catch (JsonException)
        {
            // If parameters are not valid JSON, use base cost
        }
        
        return (int)Math.Ceiling(baseCost * parameterModifier);
    }

    private async Task<string> ExecutePluginViaSemanticKernel(
        Domain.Entities.PluginEntity plugin, 
        string parameters, 
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: Implement actual plugin execution via Semantic Kernel
            // This is a placeholder implementation
            // The actual implementation would:
            // 1. Load the plugin from the Semantic Kernel plugin registry
            // 2. Parse and validate the parameters
            // 3. Execute the appropriate plugin function
            // 4. Return the results
            
            logger.LogInformation("Executing plugin {PluginName} with parameters: {Parameters}", 
                plugin.Name, parameters);

            // Parse parameters to validate they're well-formed
            Dictionary<string, object> parsedParams = new();
            try
            {
                var paramsJson = JsonDocument.Parse(parameters);
                foreach (var property in paramsJson.RootElement.EnumerateObject())
                {
                    parsedParams[property.Name] = property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString()!,
                        JsonValueKind.Number => property.Value.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Array => property.Value.GetRawText(),
                        JsonValueKind.Object => property.Value.GetRawText(),
                        _ => property.Value.GetRawText()
                    };
                }
            }
            catch (JsonException)
            {
                // If parameters are not JSON, treat as a single string parameter
                parsedParams["input"] = parameters;
            }

            // For now, return a placeholder result
            var result = new
            {
                success = true,
                pluginName = plugin.Name,
                parameters = parsedParams,
                result = "Plugin execution simulated successfully",
                timestamp = DateTime.UtcNow
            };

            return JsonSerializer.Serialize(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing plugin {PluginName} via Semantic Kernel", plugin.Name);
            throw new InvalidOperationException($"Plugin execution failed: {ex.Message}", ex);
        }
    }
}