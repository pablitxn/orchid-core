using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence.Seeders;

/// <summary>
/// Background service that handles database seeding asynchronously on startup.
/// This allows the application to start accepting requests immediately while
/// seeding runs in the background.
/// </summary>
public sealed class DatabaseSeedingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISeedingStatusService _seedingStatus;
    private readonly ILogger<DatabaseSeedingService> _logger;
    private readonly IHostEnvironment _environment;

    public DatabaseSeedingService(
        IServiceProvider serviceProvider,
        ISeedingStatusService seedingStatus,
        ILogger<DatabaseSeedingService> logger,
        IHostEnvironment environment)
    {
        _serviceProvider = serviceProvider;
        _seedingStatus = seedingStatus;
        _logger = logger;
        _environment = environment;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Skip seeding in testing environment
        if (_environment.IsEnvironment("Testing"))
        {
            _logger.LogInformation("Skipping database seeding in Testing environment");
            _seedingStatus.MarkComplete();
            return;
        }

        try
        {
            _logger.LogInformation("Starting database seeding in background...");

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            var fileStorage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            await DatabaseSeeder.SeedAsync(db, hasher, fileStorage, scope.ServiceProvider);

            _seedingStatus.MarkComplete();
            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database seeding failed");
            _seedingStatus.MarkFailed(ex);
        }
    }
}
