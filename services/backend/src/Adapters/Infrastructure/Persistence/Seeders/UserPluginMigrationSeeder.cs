using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Temporary migration service that assigns public agent plugins to existing users.
/// This ensures users can chat with public agents that have plugins.
///
/// TODO: Remove this service before production release - it's only needed for
/// retroactive migration of existing dev/staging environments.
/// </summary>
public class UserPluginMigrationSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserPluginMigrationSeeder> _logger;

    public UserPluginMigrationSeeder(IServiceProvider serviceProvider, ILogger<UserPluginMigrationSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            _logger.LogInformation("Starting user plugin migration...");

            // Get all public agents that have plugins
            var publicAgents = await dbContext.Agents
                .Where(a => a.IsPublic && !a.IsDeleted && a.PluginIds != null && a.PluginIds.Length > 0)
                .ToListAsync(cancellationToken);

            if (publicAgents.Count == 0)
            {
                _logger.LogInformation("No public agents with plugins found. Migration skipped.");
                return;
            }

            // Collect unique plugin IDs
            var pluginIds = publicAgents
                .SelectMany(a => a.PluginIds)
                .Distinct()
                .ToList();

            _logger.LogInformation("Found {PluginCount} unique plugins from {AgentCount} public agents",
                pluginIds.Count, publicAgents.Count);

            // Get all users
            var users = await dbContext.Users.ToListAsync(cancellationToken);

            var totalAssigned = 0;

            foreach (var user in users)
            {
                // Get user's existing plugins
                var existingPluginIds = await dbContext.UserPlugins
                    .Where(up => up.UserId == user.Id)
                    .Select(up => up.PluginId)
                    .ToListAsync(cancellationToken);

                // Find missing plugins
                var missingPluginIds = pluginIds.Except(existingPluginIds).ToList();

                if (missingPluginIds.Count == 0)
                {
                    continue;
                }

                // Assign missing plugins
                foreach (var pluginId in missingPluginIds)
                {
                    var userPlugin = new UserPluginEntity
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        PluginId = pluginId,
                        PurchasedAt = DateTime.UtcNow,
                        IsActive = true,
                        ExpiresAt = null
                    };

                    dbContext.UserPlugins.Add(userPlugin);
                    totalAssigned++;
                }

                _logger.LogDebug("Assigned {Count} plugins to user {UserId}", missingPluginIds.Count, user.Id);
            }

            if (totalAssigned > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("User plugin migration completed. Assigned {TotalCount} plugins to users.", totalAssigned);
            }
            else
            {
                _logger.LogInformation("User plugin migration completed. All users already have required plugins");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during user plugin migration");
            // Don't throw - migration failure shouldn't prevent app startup
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
