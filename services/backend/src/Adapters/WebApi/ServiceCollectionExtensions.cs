using Application.Interfaces;
using WebApi.Adapters;

namespace WebApi;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register WebApi-specific adapters and services
    /// </summary>
    public static IServiceCollection AddWebApiAdapters(this IServiceCollection services)
    {
        // Register SignalR adapters
        services.AddScoped<ICreditNotificationPort, SignalRCreditNotificationAdapter>();
        services.AddScoped<IRealtimeNotificationPort, SignalRNotificationAdapter>();
        
        return services;
    }
}