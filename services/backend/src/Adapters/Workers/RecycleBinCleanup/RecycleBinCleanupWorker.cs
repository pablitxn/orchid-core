using Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Workers.RecycleBinCleanup;

public class RecycleBinCleanupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecycleBinCleanupWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6); // Check every 6 hours

    public RecycleBinCleanupWorker(
        IServiceProvider serviceProvider,
        ILogger<RecycleBinCleanupWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Recycle Bin Cleanup Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredAgentsAsync(stoppingToken);
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Recycle Bin Cleanup Worker");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Wait before retry
            }
        }

        _logger.LogInformation("Recycle Bin Cleanup Worker stopped");
    }

    private async Task CleanupExpiredAgentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var agentRepository = scope.ServiceProvider.GetRequiredService<IAgentRepository>();

        _logger.LogInformation("Starting recycle bin cleanup");

        var expiredAgents = await agentRepository.ListExpiredRecycleBinAsync(cancellationToken);
        
        if (expiredAgents.Count == 0)
        {
            _logger.LogInformation("No expired agents found in recycle bin");
            return;
        }

        _logger.LogInformation("Found {Count} expired agents to permanently delete", expiredAgents.Count);

        foreach (var agent in expiredAgents)
        {
            try
            {
                // Mark as permanently deleted (not in recycle bin anymore)
                agent.IsInRecycleBin = false;
                agent.UpdatedAt = DateTime.UtcNow;
                
                await agentRepository.UpdateAsync(agent, cancellationToken);
                
                _logger.LogInformation(
                    "Permanently deleted agent {AgentId} ({AgentName}) from recycle bin", 
                    agent.Id, 
                    agent.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to permanently delete agent {AgentId} ({AgentName}) from recycle bin", 
                    agent.Id, 
                    agent.Name);
            }
        }

        _logger.LogInformation("Recycle bin cleanup completed");
    }
}