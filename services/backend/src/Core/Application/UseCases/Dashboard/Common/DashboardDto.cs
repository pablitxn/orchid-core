using System;
using System.Collections.Generic;

namespace Core.Application.UseCases.Dashboard.Common;

public sealed record DashboardOverviewDto
{
    public CreditMetricsDto Credits { get; init; } = null!;
    public SubscriptionMetricsDto Subscription { get; init; } = null!;
    public ActivityMetricsDto Activity { get; init; } = null!;
    public AgentMetricsDto Agents { get; init; } = null!;
    public UsageStatsDto Usage { get; init; } = null!;
}

public sealed record CreditMetricsDto
{
    public int CurrentBalance { get; init; }
    public int ConsumedToday { get; init; }
    public int ConsumedThisWeek { get; init; }
    public int ConsumedThisMonth { get; init; }
    public decimal AverageDailyConsumption { get; init; }
    public int DaysUntilExhaustion { get; init; }
    public List<CreditConsumptionByTypeDto> ConsumptionByType { get; init; } = new();
    public List<TopResourceConsumptionDto> TopConsumingResources { get; init; } = new();
    public bool LowCreditAlert { get; init; }
    public int AlertThreshold { get; init; }
}

public sealed record CreditConsumptionByTypeDto
{
    public string Type { get; init; } = string.Empty;
    public int CreditsConsumed { get; init; }
    public decimal Percentage { get; init; }
}

public sealed record TopResourceConsumptionDto
{
    public string ResourceId { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public string ResourceType { get; init; } = string.Empty;
    public int CreditsConsumed { get; init; }
}

public sealed record SubscriptionMetricsDto
{
    public string PlanName { get; init; } = string.Empty;
    public int IncludedCredits { get; init; }
    public DateTime ExpiresAt { get; init; }
    public int DaysUntilExpiration { get; init; }
    public bool AutoRenew { get; init; }
    public decimal Price { get; init; }
    public string PaymentUnit { get; init; } = string.Empty;
    public string RecommendedPlan { get; init; } = string.Empty;
}

public sealed record ActivityMetricsDto
{
    public int TotalChatSessions { get; init; }
    public int ActiveSessions { get; init; }
    public int ArchivedSessions { get; init; }
    public decimal AverageMessagesPerSession { get; init; }
    public TimeSpan AverageSessionDuration { get; init; }
    public List<SessionsByAgentDto> SessionsByAgent { get; init; } = new();
    public List<SessionsByTeamDto> SessionsByTeam { get; init; } = new();
    public List<ActivityByHourDto> ActivityByHour { get; init; } = new();
}

public sealed record SessionsByAgentDto
{
    public Guid AgentId { get; init; }
    public string AgentName { get; init; } = string.Empty;
    public int SessionCount { get; init; }
    public decimal Percentage { get; init; }
}

public sealed record SessionsByTeamDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = string.Empty;
    public int SessionCount { get; init; }
    public decimal Percentage { get; init; }
}

public sealed record ActivityByHourDto
{
    public int Hour { get; init; }
    public int MessageCount { get; init; }
}

public sealed record AgentMetricsDto
{
    public int TotalAgents { get; init; }
    public int ActiveAgents { get; init; }
    public int DeletedAgents { get; init; }
    public List<TopAgentDto> TopAgents { get; init; } = new();
    public List<AgentLanguageDistributionDto> LanguageDistribution { get; init; } = new();
}

public sealed record TopAgentDto
{
    public Guid AgentId { get; init; }
    public string AgentName { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
    public int SessionCount { get; init; }
    public int MessageCount { get; init; }
    public decimal AverageResponseTime { get; init; }
}

public sealed record AgentLanguageDistributionDto
{
    public string Language { get; init; } = string.Empty;
    public int Count { get; init; }
    public decimal Percentage { get; init; }
}

public sealed record UsageStatsDto
{
    public PluginUsageDto Plugins { get; init; } = null!;
    public WorkflowUsageDto Workflows { get; init; } = null!;
    public KnowledgeBaseUsageDto KnowledgeBase { get; init; } = null!;
    public MediaCenterUsageDto MediaCenter { get; init; } = null!;
}

public sealed record PluginUsageDto
{
    public int TotalPurchased { get; init; }
    public int ActivePlugins { get; init; }
    public List<TopPluginDto> TopPlugins { get; init; } = new();
    public int SubscriptionBasedCount { get; init; }
    public int OneTimePurchaseCount { get; init; }
}

public sealed record TopPluginDto
{
    public Guid PluginId { get; init; }
    public string PluginName { get; init; } = string.Empty;
    public int UsageCount { get; init; }
    public int CreditsConsumed { get; init; }
}

public sealed record WorkflowUsageDto
{
    public int TotalPurchased { get; init; }
    public int ExecutedThisMonth { get; init; }
    public decimal SuccessRate { get; init; }
    public List<TopWorkflowDto> TopWorkflows { get; init; } = new();
}

public sealed record TopWorkflowDto
{
    public Guid WorkflowId { get; init; }
    public string WorkflowName { get; init; } = string.Empty;
    public int ExecutionCount { get; init; }
    public decimal SuccessRate { get; init; }
}

public sealed record KnowledgeBaseUsageDto
{
    public int TotalDocuments { get; init; }
    public long TotalSizeBytes { get; init; }
    public int IndexedDocuments { get; init; }
    public Dictionary<string, int> FileTypeDistribution { get; init; } = new();
    public List<string> TopTags { get; init; } = new();
}

public sealed record MediaCenterUsageDto
{
    public int TotalAssets { get; init; }
    public long TotalSizeBytes { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public Dictionary<string, int> MediaTypeDistribution { get; init; } = new();
}

// Detailed metrics DTOs
public sealed record CreditHistoryDto
{
    public List<CreditHistoryItemDto> Items { get; init; } = new();
    public int TotalPages { get; init; }
    public int CurrentPage { get; init; }
}

public sealed record CreditHistoryItemDto
{
    public Guid Id { get; init; }
    public string ConsumptionType { get; init; } = string.Empty;
    public string ResourceName { get; init; } = string.Empty;
    public int CreditsConsumed { get; init; }
    public int BalanceAfter { get; init; }
    public DateTime ConsumedAt { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

public sealed record SessionDetailsDto
{
    public Guid SessionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public Guid? AgentId { get; init; }
    public string? AgentName { get; init; }
    public Guid? TeamId { get; init; }
    public string? TeamName { get; init; }
    public int MessageCount { get; init; }
    public int CreditsConsumed { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastMessageAt { get; init; }
    public bool IsArchived { get; init; }
}

public sealed record BillingHistoryDto
{
    public List<BillingHistoryItemDto> Items { get; init; } = new();
    public decimal TotalSpent { get; init; }
    public int TotalPages { get; init; }
    public int CurrentPage { get; init; }
}

public sealed record BillingHistoryItemDto
{
    public Guid Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? InvoiceUrl { get; init; }
}

public sealed record SecurityMetricsDto
{
    public DateTime? LastLogin { get; init; }
    public List<LoginHistoryDto> RecentLogins { get; init; } = new();
    public int FailedLoginAttempts { get; init; }
    public DateTime? LastPasswordChange { get; init; }
    public List<string> ActiveDevices { get; init; } = new();
}

public sealed record LoginHistoryDto
{
    public DateTime Timestamp { get; init; }
    public string IpAddress { get; init; } = string.Empty;
    public string? UserAgent { get; init; }
    public string? Location { get; init; }
    public bool Success { get; init; }
}