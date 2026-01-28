using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using Application.Interfaces;
using Infrastructure.Ai.AgentFramework.Abstractions;
using Infrastructure.Ai.AgentFramework.Services;
using Infrastructure.Ai.AgentFramework.Tools;
using Infrastructure.Http;
using Orchid.Infrastructure.Ai.AgentFramework;

namespace Infrastructure.Ai.AgentFramework;

/// <summary>
/// Registers Microsoft Agent Framework services and exposes them through <see cref="IChatCompletionPort"/>.
/// </summary>
public static class AgentFrameworkServiceCollectionExtensions
{
    /// <summary>
    /// Adds Microsoft Agent Framework services to the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentFramework(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── 1. Validation ───────────────────────────────────────────────
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                     ?? configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "OpenAI API key missing. Set OpenAI:ApiKey or the OPENAI_API_KEY env-var.");

        var defaultModel = configuration["OpenAI:Model"] ?? "gpt-5-nano-2025-08-07";

        // ─── 2. OpenAI Client (singleton, thread-safe) ──────────────────
        services.AddSingleton(_ => new OpenAIClient(apiKey));

        // ─── 3. IChatClient factory (scoped per request) ────────────────
        services.AddScoped<IChatClient>(sp =>
        {
            var openAIClient = sp.GetRequiredService<OpenAIClient>();
            return openAIClient
                .GetChatClient(defaultModel)
                .AsIChatClient();
        });

        // ─── 4. Baseline infrastructure needed by tools ─────────────────
        services.AddMemoryCache();
        services.AddDistributedMemoryCache();

        // Register the correlation ID handler for HTTP requests
        services.AddTransient<CorrelationIdDelegatingHandler>();

        // Configure default HttpClient with correlation ID propagation
        services.AddHttpClient()
            .ConfigureHttpClientDefaults(builder =>
            {
                builder.AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
            });

        // ─── 5. Agent factory for model-per-agent support ───────────────
        services.AddScoped<IAgentFactory>(sp => new AgentFactory(
            sp.GetRequiredService<OpenAIClient>(),
            defaultModel));

        // ─── 6. Tool registry for tool discovery ───────────────────────────
        // Use factory to inject optional OpenAPI dependencies for dynamic tool resolution
        services.AddScoped<IToolRegistry>(sp => new Services.ToolRegistry(
            sp,
            sp.GetRequiredService<ILogger<Services.ToolRegistry>>(),
            sp.GetService<IOpenApiToolFactory>(),      // Optional: enables OpenAPI tool resolution
            sp.GetService<IPluginRepository>()         // Optional: enables DB lookup for plugins
        ));

        // ─── 7. Tool loader for dynamic agent tools ───────────────────────
        // Use factory to inject optional telemetry client for tool invocation tracking,
        // optional credit tracking service for billing, and OpenAPI tool factory
        services.AddScoped<IAgentToolLoader>(sp => new Services.AgentToolLoader(
            sp.GetRequiredService<IPluginRepository>(),
            sp.GetRequiredService<IToolRegistry>(),
            sp.GetRequiredService<ILogger<Services.AgentToolLoader>>(),
            sp.GetService<IEnhancedTelemetryClient>(), // Optional telemetry client
            sp.GetService<ICreditTrackingService>(),   // Optional credit tracking service
            sp.GetService<IOpenApiToolFactory>(),      // Optional OpenAPI tool factory
            enableSafeWrapping: true,
            enableTelemetryWrapping: true,
            enableCreditTracking: true
        ));

        // ─── 8. Thread manager for conversation state ────────────────────
        // Singleton: maintains in-memory thread state across requests
        services.AddSingleton<IAgentThreadManager, Services.AgentThreadManager>();

        // ─── 8.1 Thread manager configuration & cleanup service ──────────
        services.Configure<ThreadManagerSettings>(
            configuration.GetSection(ThreadManagerSettings.SectionName));
        services.AddHostedService<Services.ThreadCleanupBackgroundService>();

        // ─── 9. Chat adapter ➜ IChatCompletionPort forwarding ───────────
        services.AddScoped<AgentFrameworkChatCompletionAdapter>();
        services.AddScoped<IChatCompletionPort>(sp =>
            sp.GetRequiredService<AgentFrameworkChatCompletionAdapter>());

        return services;
    }

    /// <summary>
    /// Registers Agent Framework tools (equivalent to SK plugins).
    /// </summary>
    public static IServiceCollection AddAgentFrameworkTools(this IServiceCollection services)
    {
        return services
            .AddTransient<VectorStoreTool>()
            .AddTransient<WebSearchTool>()
            .AddScoped<IOpenApiToolFactory>(sp => new OpenApiToolFactory(
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<IActivityPublisher>(),
                sp.GetRequiredService<IPluginRepository>(),
                sp.GetRequiredService<ILogger<OpenApiToolFactory>>(),
                sp.GetRequiredService<ILogger<OpenApiTool>>()))
            .AddScoped<IOpenApiSpecService>(sp => new OpenApiSpecService(
                sp.GetRequiredService<IOpenApiToolFactory>(),
                sp.GetRequiredService<IHttpClientFactory>(),
                sp.GetRequiredService<ILogger<OpenApiSpecService>>()));
    }
}
