using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

public class PluginSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PluginSeeder> _logger;

    public PluginSeeder(IServiceProvider serviceProvider, ILogger<PluginSeeder> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IPluginDiscoveryService>();

        try
        {
            // Ensure database is migrated
            await dbContext.Database.MigrateAsync(cancellationToken);

            // Discover plugins from Semantic Kernel
            var discoveredPlugins = await discoveryService.DiscoverPluginsAsync(cancellationToken);

            foreach (var discovered in discoveredPlugins)
            {
                var existingPlugin = await dbContext.Plugins
                    .FirstOrDefaultAsync(p => p.SystemName == discovered.SystemName, cancellationToken);

                if (existingPlugin == null)
                {
                    // Create new plugin
                    var plugin = new PluginEntity
                    {
                        Id = Guid.NewGuid(),
                        Name = discovered.Name,
                        SystemName = discovered.SystemName,
                        Description = discovered.Description,
                        PriceCredits = discovered.DefaultPriceCredits,
                        IsSubscriptionBased = discovered.IsSubscriptionBased,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    dbContext.Plugins.Add(plugin);
                    _logger.LogInformation("Seeded new plugin: {PluginName} ({SystemName})", plugin.Name, plugin.SystemName);
                }
                else
                {
                    // Update existing plugin metadata (but preserve pricing if manually changed)
                    existingPlugin.Name = discovered.Name;
                    existingPlugin.Description = discovered.Description;
                    existingPlugin.UpdatedAt = DateTime.UtcNow;
                    
                    _logger.LogInformation("Updated plugin metadata: {PluginName} ({SystemName})", existingPlugin.Name, existingPlugin.SystemName);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Plugin seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin seeding");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}