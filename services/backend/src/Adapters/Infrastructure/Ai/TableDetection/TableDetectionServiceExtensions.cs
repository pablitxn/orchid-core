using Application.Interfaces;
using Application.Interfaces.Spreadsheet;
using Infrastructure.Logging;
using Infrastructure.Persistence;
using Infrastructure.Telemetry.Ai.Spreadsheet;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Ai.TableDetection;

/// <summary>
/// Extension methods for registering table detection and related services.
/// </summary>
public static class TableDetectionServiceExtensions
{
    /// <summary>
    /// Adds table detection service with observability.
    /// </summary>
    private static void AddTableDetection(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<TableDetectionOptions>(options =>
        {
            options.MaxRetries = configuration.GetValue("TableDetection:MaxRetries", 3);
            options.InputTokenCostPer1K = configuration.GetValue("TableDetection:InputTokenCostPer1K", 0.01m);
            options.OutputTokenCostPer1K = configuration.GetValue("TableDetection:OutputTokenCostPer1K", 0.03m);
            options.MaxContextLength = configuration.GetValue("TableDetection:MaxContextLength", 128000);
        });

        // Register table detection service
        services.AddScoped<ITableDetectionService, TableDetectionService>();

        // Register cost tracking
        services.AddScoped<IActionCostRepository, ActionCostRepository>();
    }

    /// <summary>
    /// Adds enhanced observability for spreadsheet operations.
    /// </summary>
    private static void AddSpreadsheetObservability(this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure safe logger options
        services.Configure<SafeLoggerOptions>(options =>
        {
            options.MaxTextLength = configuration.GetValue<int>("Logging:MaxTextLength", 128);
            options.NumericThreshold = configuration.GetValue<double>("Logging:NumericThreshold", 100);
        });

        // Register safe logger factory
        services.AddSingleton<ISafeLogger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<SafeLogger>();
            var options = provider.GetService<Microsoft.Extensions.Options.IOptions<SafeLoggerOptions>>()?.Value;
            return new SafeLogger(logger, options);
        });

        // Add spreadsheet telemetry behavior
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(SpreadsheetTelemetryBehavior<,>));
    }

    /// <summary>
    /// Adds all spreadsheet AI capabilities including Chain of Spreadsheet.
    /// </summary>
    public static IServiceCollection AddSpreadsheetAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add table detection
        services.AddTableDetection(configuration);

        // Add observability
        services.AddSpreadsheetObservability(configuration);

        // Ensure telemetry is configured
        if (services.All(x => x.ServiceType != typeof(ITelemetryClient)))
        {
            services.AddLangfuseTelemetry(configuration);
        }

        return services;
    }
}