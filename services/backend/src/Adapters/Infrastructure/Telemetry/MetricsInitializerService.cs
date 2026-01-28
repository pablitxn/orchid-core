using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Telemetry;

/// <summary>
/// Initializes Prometheus metrics with zero values at application startup.
/// This ensures metrics appear in /metrics endpoint immediately (showing "0")
/// rather than "No data" in Grafana dashboards until first use.
/// </summary>
public sealed class MetricsInitializerService : IHostedService
{
    private readonly ILogger<MetricsInitializerService> _logger;

    public MetricsInitializerService(ILogger<MetricsInitializerService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Initializing Prometheus metrics with baseline values");

        // Initialize counters with placeholder labels so they appear in /metrics
        // Using "_unset" as a clear indicator these are baseline values
        const string unset = "_unset";

        // Chat & Messaging Metrics
        ApplicationMetrics.ChatMessagesTotal.WithLabels(unset, "success").Inc(0);
        ApplicationMetrics.ChatMessagesTotal.WithLabels(unset, "error").Inc(0);
        ApplicationMetrics.ChatMessageDuration.WithLabels(unset).Observe(0);

        // AI & Token Metrics
        ApplicationMetrics.AiTokensTotal.WithLabels(unset, "input").Inc(0);
        ApplicationMetrics.AiTokensTotal.WithLabels(unset, "output").Inc(0);
        ApplicationMetrics.AiOperationsTotal.WithLabels("chat", unset, "success").Inc(0);
        ApplicationMetrics.AiOperationsTotal.WithLabels("chat", unset, "error").Inc(0);
        ApplicationMetrics.AiOperationDuration.WithLabels("chat", unset).Observe(0);

        // Credit System Metrics
        ApplicationMetrics.CreditsConsumedTotal.WithLabels("chat", unset).Inc(0);
        ApplicationMetrics.CreditsConsumedTotal.WithLabels("tool", unset).Inc(0);
        ApplicationMetrics.CreditBalanceGauge.WithLabels("free").Set(0);
        ApplicationMetrics.CreditBalanceGauge.WithLabels("pro").Set(0);

        // Tool Invocation Metrics
        ApplicationMetrics.ToolInvocationsTotal.WithLabels(unset, "success").Inc(0);
        ApplicationMetrics.ToolInvocationsTotal.WithLabels(unset, "error").Inc(0);
        ApplicationMetrics.ToolExecutionDuration.WithLabels(unset).Observe(0);

        // Knowledge Base Metrics
        ApplicationMetrics.EmbeddingsTotal.WithLabels("success").Inc(0);
        ApplicationMetrics.EmbeddingsTotal.WithLabels("error").Inc(0);
        ApplicationMetrics.VectorSearchesTotal.WithLabels("success").Inc(0);
        ApplicationMetrics.VectorSearchesTotal.WithLabels("error").Inc(0);
        ApplicationMetrics.VectorSearchDuration.Observe(0);

        // Workflow Metrics
        ApplicationMetrics.WorkflowExecutionsTotal.WithLabels(unset, "success").Inc(0);
        ApplicationMetrics.WorkflowExecutionsTotal.WithLabels(unset, "error").Inc(0);
        ApplicationMetrics.WorkflowExecutionDuration.WithLabels(unset).Observe(0);

        // User Activity Metrics
        ApplicationMetrics.ActiveUsersGauge.WithLabels("1h").Set(0);
        ApplicationMetrics.ActiveUsersGauge.WithLabels("24h").Set(0);
        ApplicationMetrics.UserRegistrationsTotal.WithLabels("email").Inc(0);
        ApplicationMetrics.UserRegistrationsTotal.WithLabels("google").Inc(0);
        ApplicationMetrics.UserRegistrationsTotal.WithLabels("auth0").Inc(0);

        _logger.LogInformation("Prometheus metrics initialized successfully");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
