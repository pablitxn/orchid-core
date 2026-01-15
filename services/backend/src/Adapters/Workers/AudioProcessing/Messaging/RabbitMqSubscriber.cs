using AudioProcessingWorker.Handlers;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AudioProcessingWorker.Messaging;

public static class RabbitMqSubscriber
{
    public static IServiceCollection AddRabbitMqConsumer(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMassTransit(config =>
        {
            config.AddConsumer<ProjectCreatedHandler>();

            config.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var username = configuration["RabbitMq:Username"] ?? "guest";
                var password = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(username);
                    h.Password(password);
                });

                cfg.ReceiveEndpoint("project-created-queue",
                    e => { e.ConfigureConsumer<ProjectCreatedHandler>(context); });
            });
        });

        services.AddMassTransitHostedService(true);

        return services;
    }
}