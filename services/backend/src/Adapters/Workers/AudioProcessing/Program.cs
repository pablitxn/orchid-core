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
                        var host = ctx.Configuration["RabbitMq:Host"] ?? "localhost";
                        var username = ctx.Configuration["RabbitMq:Username"] ?? "guest";
                        var password = ctx.Configuration["RabbitMq:Password"] ?? "guest";

                        cfg.Host(host, "/", h =>
                        {
                            h.Username(username);
                            h.Password(password);
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