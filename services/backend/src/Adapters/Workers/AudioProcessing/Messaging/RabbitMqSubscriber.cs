using AudioProcessingWorker.Handlers;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;

namespace AudioProcessingWorker.Messaging;

public static class RabbitMqSubscriber
{
    public static IServiceCollection AddRabbitMqConsumer(this IServiceCollection services)
    {
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

                cfg.ReceiveEndpoint("project-created-queue",
                    e => { e.ConfigureConsumer<ProjectCreatedHandler>(context); });
            });
        });

        services.AddMassTransitHostedService(true);

        return services;
    }
}