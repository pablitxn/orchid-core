using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Infrastructure.Ai.SemanticKernel.Plugins;
using Application.Interfaces;

namespace Infrastructure.Ai.SemanticKernel;

/// <summary>
/// Registers Semantic Kernel and exposes it through <see cref="IChatCompletionPort"/>.
/// </summary>
public static class SemanticKernelServiceCollectionExtensions
{
    public static IServiceCollection AddSemanticKernel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── 1. Validation ───────────────────────────────────────────────
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "OpenAI API key missing. Set OpenAI:ApiKey or the OPENAI_API_KEY env-var.");

        var modelId = configuration["OpenAI:Model"] ?? "o4-mini";

        // ─── 2. Chat-completion connector (DI flavour) ───────────────────
        services.AddOpenAIChatCompletion(modelId, apiKey); // SK extension 

        // ─── 3. Baseline infrastructure needed by plugins ────────────────
        services.AddMemoryCache();
        services.AddDistributedMemoryCache(); // no-op if already added
        services.AddHttpClient();

        // ─── 4. Kernel per request/connection ────────────────────────────
        services.AddScoped<Kernel>(sp =>
        {
            var kernel = new Kernel(sp);
            kernel.ImportPluginFromType<SpreadsheetPluginV3Refactored>("excel");
            kernel.ImportPluginFromType<VectorStorePlugin>("vector_store");
            kernel.ImportPluginFromType<MathEnginePlugin>("math");
            return kernel;
        });

        // ─── 5. Support components ───────────────────────────────────────
        services.AddScoped<IAgentPluginLoader, Services.AgentPluginLoader>();

        // ─── 6. Chat adapter  ➜  IChatCompletionPort forwarding ───────────
        services.AddScoped<SemanticKernelChatCompletionAdapter>();
        services.AddScoped<IChatCompletionPort>(sp =>
            sp.GetRequiredService<SemanticKernelChatCompletionAdapter>());

        return services;
    }
    
    public static IServiceCollection AddSemanticKernelPlugins(this IServiceCollection services)
    {
        return services
            .AddTransient<SpreadsheetPluginV3Refactored>()
            .AddTransient<VectorStorePlugin>()
            .AddTransient<MathEnginePlugin>()
            .AddTransient<WebSearchPlugin>();
    }

}