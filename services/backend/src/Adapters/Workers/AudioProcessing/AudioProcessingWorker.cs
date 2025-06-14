using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioProcessingWorker;

public class AudioProcessingWorker(ILogger<AudioProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aquí podrías poner lógica que corra en segundo plano
        // distinta de la mensajería.
        logger.LogInformation("AudioProcessingWorker running.");

        while (!stoppingToken.IsCancellationRequested)
            // Por ejemplo, un loop con pequeñas esperas
            // para realizar otras tareas periodicamente
            await Task.Delay(5000, stoppingToken);
    }
}