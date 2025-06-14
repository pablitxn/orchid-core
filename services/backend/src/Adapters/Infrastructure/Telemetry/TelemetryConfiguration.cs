using Application.Interfaces;
using Infrastructure.Telemetry.Ai.Langfuse;
using Infrastructure.Telemetry.Ai.NoOpTelemetryClient;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;

namespace Infrastructure.Telemetry;

/// <summary>
/// Telemetry configuration extensions for routing AI and non-AI telemetry
/// </summary>
public static class TelemetryConfiguration
{
    /// <summary>
    /// Adds telemetry services with proper routing for AI vs non-AI telemetry
    /// </summary>
    public static IServiceCollection AddTelemetryServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var langfuseConfig = configuration.GetSection("Langfuse");
        var enableAiTelemetry = langfuseConfig.GetValue<bool>("Enabled", false);
        var enableNonAiTelemetry = configuration.GetValue<bool>("Telemetry:NonAI:Enabled", false);

        if (enableAiTelemetry)
        {
            // Configure Langfuse for AI telemetry
            services.AddLangfuseAiTelemetry(configuration);
        }
        else
        {
            // Use NoOp for AI telemetry when Langfuse is disabled
            services.AddSingleton<IEnhancedTelemetryClient>(sp =>
                new EnhancedNoOpTelemetryClient(sp.GetRequiredService<ILogger<EnhancedNoOpTelemetryClient>>()));
        }

        if (enableNonAiTelemetry)
        {
            // Configure traditional telemetry (e.g., Application Insights, OpenTelemetry)
            services.AddNonAiTelemetry(configuration);
        }
        else
        {
            // Use NoOp for non-AI telemetry
            services.AddSingleton<ITelemetryClient>(sp =>
                new NoOpTelemetryClient());
        }

        // Register telemetry router
        services.AddSingleton<ITelemetryRouter, TelemetryRouter>();

        return services;
    }

    /// <summary>
    /// Configures Langfuse for AI-specific telemetry
    /// </summary>
    private static IServiceCollection AddLangfuseAiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var publicKey = configuration["Langfuse:PublicKey"];
        var secretKey = configuration["Langfuse:SecretKey"];
        var baseUrl = configuration["Langfuse:BaseUrl"] ?? "https://cloud.langfuse.com";

        if (string.IsNullOrWhiteSpace(publicKey) || string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "Langfuse is enabled but PublicKey or SecretKey is missing in configuration");
        }

        // Configure HttpClient for Langfuse
        services.AddHttpClient<IEnhancedTelemetryClient, EnhancedLangfuseClient>(client =>
        {
            // Ensure base URL ends with a trailing slash
            var normalizedBaseUrl = baseUrl.TrimEnd('/') + '/';
            client.BaseAddress = new Uri(normalizedBaseUrl);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes($"{publicKey}:{secretKey}")));
            client.DefaultRequestHeaders.Add("User-Agent", "playground-dotnet/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register AI-specific telemetry behaviors
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AiTelemetryBehavior<,>));

        return services;
    }

    /// <summary>
    /// Configures traditional telemetry for non-AI operations
    /// </summary>
    private static IServiceCollection AddNonAiTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // For now, we'll use NoOp telemetry for non-AI operations
        // In the future, this could be replaced with Application Insights,
        // OpenTelemetry, or other traditional telemetry providers
        services.AddSingleton<ITelemetryClient>(sp =>
            new NoOpTelemetryClient());

        // Register non-AI telemetry behaviors
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(NonAiTelemetryBehavior<,>));

        return services;
    }
}

/// <summary>
/// Interface for routing telemetry based on operation type
/// </summary>
public interface ITelemetryRouter
{
    ITelemetryClient GetTelemetryClient(bool isAiOperation);
    IEnhancedTelemetryClient? GetEnhancedTelemetryClient();
}

/// <summary>
/// Routes telemetry to appropriate clients based on operation type
/// </summary>
public class TelemetryRouter : ITelemetryRouter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TelemetryRouter> _logger;

    public TelemetryRouter(IServiceProvider serviceProvider, ILogger<TelemetryRouter> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public ITelemetryClient GetTelemetryClient(bool isAiOperation)
    {
        if (isAiOperation)
        {
            // Try to get enhanced client first, fall back to regular client
            var enhancedClient = _serviceProvider.GetService<IEnhancedTelemetryClient>();
            if (enhancedClient != null)
            {
                return enhancedClient;
            }
        }

        // Return regular telemetry client for non-AI operations
        return _serviceProvider.GetRequiredService<ITelemetryClient>();
    }

    public IEnhancedTelemetryClient? GetEnhancedTelemetryClient()
    {
        return _serviceProvider.GetService<IEnhancedTelemetryClient>();
    }
}

/// <summary>
/// Telemetry behavior for AI operations
/// </summary>
public class AiTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITelemetryRouter _telemetryRouter;
    private readonly ILogger<AiTelemetryBehavior<TRequest, TResponse>> _logger;

    public AiTelemetryBehavior(ITelemetryRouter telemetryRouter, ILogger<AiTelemetryBehavior<TRequest, TResponse>> logger)
    {
        _telemetryRouter = telemetryRouter;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Check if this is an AI-related operation
        if (!TelemetryOperationClassifier.IsAiOperation(request))
        {
            return await next();
        }

        var telemetryClient = _telemetryRouter.GetEnhancedTelemetryClient() ?? 
                             _telemetryRouter.GetTelemetryClient(true);

        var requestName = typeof(TRequest).Name;
        var traceId = await telemetryClient.StartTraceAsync($"AI Operation: {requestName}", request, cancellationToken);

        try
        {
            var response = await next();
            await telemetryClient.EndTraceAsync(traceId, true, new { response }, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await telemetryClient.EndTraceAsync(traceId, false, new { error = ex.Message }, cancellationToken);
            throw;
        }
    }
}

/// <summary>
/// Telemetry behavior for non-AI operations
/// </summary>
public class NonAiTelemetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ITelemetryRouter _telemetryRouter;
    private readonly ILogger<NonAiTelemetryBehavior<TRequest, TResponse>> _logger;

    public NonAiTelemetryBehavior(ITelemetryRouter telemetryRouter, ILogger<NonAiTelemetryBehavior<TRequest, TResponse>> logger)
    {
        _telemetryRouter = telemetryRouter;
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        // Skip if this is an AI operation (handled by AiTelemetryBehavior)
        if (TelemetryOperationClassifier.IsAiOperation(request))
        {
            return await next();
        }

        var telemetryClient = _telemetryRouter.GetTelemetryClient(false);
        var requestName = typeof(TRequest).Name;
        var traceId = await telemetryClient.StartTraceAsync($"Operation: {requestName}", request, cancellationToken);

        try
        {
            var response = await next();
            await telemetryClient.EndTraceAsync(traceId, true, new { response }, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await telemetryClient.EndTraceAsync(traceId, false, new { error = ex.Message }, cancellationToken);
            throw;
        }
    }
}