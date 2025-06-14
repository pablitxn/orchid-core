using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioProcessingWorker;

public abstract class WorkerService(ILogger<WorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WorkerService is running.");

        while (!stoppingToken.IsCancellationRequested) await Task.Delay(5000, stoppingToken);
    }
}