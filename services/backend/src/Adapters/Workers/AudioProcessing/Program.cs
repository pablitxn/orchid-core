using AudioProcessingWorker.Handlers;
using Infrastructure;
using Infrastructure.Telemetry.Ai.Langfuse;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AudioProcessingWorker;

public static class Program
{
    [Obsolete("Obsolete")]
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((ctx, services) =>
            {
                services.AddHostedService<WorkerService>();

                services.AddLangfuseTelemetry(ctx.Configuration);

                services.AddMassTransit(config =>
                {
                    config.AddConsumer<ProjectCreatedHandler>();

                    config.UsingRabbitMq((context, cfg) =>
                    {
                        cfg.Host("localhost", "/", h =>
                        {
                            h.Username("guest");
                            h.Password("guest");
                        });

                        cfg.UseConsumeFilter(typeof(LangfuseConsumeFilter<>), context);
                    });
                });

                services.AddMassTransitHostedService(true);
            })
            .Build();

        await host.RunAsync();
    }
}