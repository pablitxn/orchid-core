using Prometheus;

namespace Infrastructure.Telemetry;

/// <summary>
/// Prometheus metrics for application-level observability.
/// Metrics follow the naming convention: app_{domain}_{metric_type}
/// </summary>
public static class ApplicationMetrics
{
    // ─── Chat & Messaging Metrics ───────────────────────────────────────────────

    /// <summary>
    /// Counter for chat messages processed by agent
    /// </summary>
    public static readonly Counter ChatMessagesTotal = Metrics.CreateCounter(
        "app_chat_messages_total",
        "Total number of chat messages processed",
        new CounterConfiguration
        {
            LabelNames = ["agent_id", "status"]
        });

    /// <summary>
    /// Histogram for chat message processing duration
    /// </summary>
    public static readonly Histogram ChatMessageDuration = Metrics.CreateHistogram(
        "app_chat_message_duration_seconds",
        "Duration of chat message processing in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["agent_id"],
            Buckets = [0.1, 0.25, 0.5, 1, 2.5, 5, 10, 30, 60]
        });

    // ─── AI & Token Metrics ─────────────────────────────────────────────────────

    /// <summary>
    /// Counter for AI tokens consumed
    /// </summary>
    public static readonly Counter AiTokensTotal = Metrics.CreateCounter(
        "app_ai_tokens_total",
        "Total number of AI tokens consumed",
        new CounterConfiguration
        {
            LabelNames = ["model", "token_type"]
        });

    /// <summary>
    /// Counter for AI operations
    /// </summary>
    public static readonly Counter AiOperationsTotal = Metrics.CreateCounter(
        "app_ai_operations_total",
        "Total number of AI operations",
        new CounterConfiguration
        {
            LabelNames = ["operation_type", "model", "status"]
        });

    /// <summary>
    /// Histogram for AI operation latency
    /// </summary>
    public static readonly Histogram AiOperationDuration = Metrics.CreateHistogram(
        "app_ai_operation_duration_seconds",
        "Duration of AI operations in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["operation_type", "model"],
            Buckets = [0.1, 0.5, 1, 2, 5, 10, 30, 60, 120]
        });

    // ─── Credit System Metrics ──────────────────────────────────────────────────

    /// <summary>
    /// Counter for credits consumed
    /// </summary>
    public static readonly Counter CreditsConsumedTotal = Metrics.CreateCounter(
        "app_credits_consumed_total",
        "Total number of credits consumed",
        new CounterConfiguration
        {
            LabelNames = ["operation_type", "tool_name"]
        });

    /// <summary>
    /// Gauge for current credit balance (per user, sampled periodically)
    /// </summary>
    public static readonly Gauge CreditBalanceGauge = Metrics.CreateGauge(
        "app_credit_balance",
        "Current credit balance",
        new GaugeConfiguration
        {
            LabelNames = ["tier"]
        });

    // ─── Tool Invocation Metrics ────────────────────────────────────────────────

    /// <summary>
    /// Counter for tool invocations
    /// </summary>
    public static readonly Counter ToolInvocationsTotal = Metrics.CreateCounter(
        "app_tool_invocations_total",
        "Total number of tool invocations",
        new CounterConfiguration
        {
            LabelNames = ["tool_name", "status"]
        });

    /// <summary>
    /// Histogram for tool execution duration
    /// </summary>
    public static readonly Histogram ToolExecutionDuration = Metrics.CreateHistogram(
        "app_tool_execution_duration_seconds",
        "Duration of tool executions in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["tool_name"],
            Buckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
        });

    // ─── Knowledge Base Metrics ─────────────────────────────────────────────────

    /// <summary>
    /// Counter for embedding operations
    /// </summary>
    public static readonly Counter EmbeddingsTotal = Metrics.CreateCounter(
        "app_embeddings_total",
        "Total number of embedding operations",
        new CounterConfiguration
        {
            LabelNames = ["status"]
        });

    /// <summary>
    /// Counter for vector store searches
    /// </summary>
    public static readonly Counter VectorSearchesTotal = Metrics.CreateCounter(
        "app_vector_searches_total",
        "Total number of vector store searches",
        new CounterConfiguration
        {
            LabelNames = ["status"]
        });

    /// <summary>
    /// Histogram for vector search latency
    /// </summary>
    public static readonly Histogram VectorSearchDuration = Metrics.CreateHistogram(
        "app_vector_search_duration_seconds",
        "Duration of vector search operations in seconds",
        new HistogramConfiguration
        {
            Buckets = [0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1]
        });

    // ─── Workflow Metrics ───────────────────────────────────────────────────────

    /// <summary>
    /// Counter for workflow executions
    /// </summary>
    public static readonly Counter WorkflowExecutionsTotal = Metrics.CreateCounter(
        "app_workflow_executions_total",
        "Total number of workflow executions",
        new CounterConfiguration
        {
            LabelNames = ["workflow_id", "status"]
        });

    /// <summary>
    /// Histogram for workflow execution duration
    /// </summary>
    public static readonly Histogram WorkflowExecutionDuration = Metrics.CreateHistogram(
        "app_workflow_execution_duration_seconds",
        "Duration of workflow executions in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["workflow_id"],
            Buckets = [1, 5, 10, 30, 60, 120, 300, 600]
        });

    // ─── User Activity Metrics ──────────────────────────────────────────────────

    /// <summary>
    /// Gauge for active users (updated periodically)
    /// </summary>
    public static readonly Gauge ActiveUsersGauge = Metrics.CreateGauge(
        "app_active_users",
        "Number of active users in the last period",
        new GaugeConfiguration
        {
            LabelNames = ["period"]
        });

    /// <summary>
    /// Counter for user registrations
    /// </summary>
    public static readonly Counter UserRegistrationsTotal = Metrics.CreateCounter(
        "app_user_registrations_total",
        "Total number of user registrations",
        new CounterConfiguration
        {
            LabelNames = ["provider"]
        });

    // ─── Thread Manager Metrics ──────────────────────────────────────────────────

    /// <summary>
    /// Gauge for currently active conversation threads in memory
    /// </summary>
    public static readonly Gauge ActiveThreadsGauge = Metrics.CreateGauge(
        "app_threads_active",
        "Number of active conversation threads in memory");

    /// <summary>
    /// Counter for total threads created
    /// </summary>
    public static readonly Counter ThreadsCreatedTotal = Metrics.CreateCounter(
        "app_threads_created_total",
        "Total number of conversation threads created");

    /// <summary>
    /// Counter for threads cleaned up
    /// </summary>
    public static readonly Counter ThreadsCleanedTotal = Metrics.CreateCounter(
        "app_threads_cleaned_total",
        "Total number of conversation threads cleaned up",
        new CounterConfiguration
        {
            LabelNames = ["reason"]
        });

    // ─── Plugin Analytics Metrics ───────────────────────────────────────────────

    /// <summary>
    /// Counter for plugin installations
    /// </summary>
    public static readonly Counter PluginInstallsTotal = Metrics.CreateCounter(
        "app_plugin_installs_total",
        "Total number of plugin installations",
        new CounterConfiguration
        {
            LabelNames = ["plugin_id", "plugin_name"]
        });

    /// <summary>
    /// Counter for plugin uninstallations
    /// </summary>
    public static readonly Counter PluginUninstallsTotal = Metrics.CreateCounter(
        "app_plugin_uninstalls_total",
        "Total number of plugin uninstallations",
        new CounterConfiguration
        {
            LabelNames = ["plugin_id", "plugin_name"]
        });

    /// <summary>
    /// Counter for plugin invocations (success/error)
    /// </summary>
    public static readonly Counter PluginInvocationsTotal = Metrics.CreateCounter(
        "app_plugin_invocations_total",
        "Total number of plugin invocations",
        new CounterConfiguration
        {
            LabelNames = ["plugin_id", "plugin_name", "status"]
        });

    /// <summary>
    /// Histogram for plugin invocation duration
    /// </summary>
    public static readonly Histogram PluginInvocationDuration = Metrics.CreateHistogram(
        "app_plugin_invocation_duration_seconds",
        "Duration of plugin invocations in seconds",
        new HistogramConfiguration
        {
            LabelNames = ["plugin_id", "plugin_name"],
            Buckets = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10]
        });

    /// <summary>
    /// Gauge for active plugin users (updated periodically)
    /// </summary>
    public static readonly Gauge PluginActiveUsersGauge = Metrics.CreateGauge(
        "app_plugin_active_users",
        "Number of active users per plugin",
        new GaugeConfiguration
        {
            LabelNames = ["plugin_id", "period"]
        });
}
