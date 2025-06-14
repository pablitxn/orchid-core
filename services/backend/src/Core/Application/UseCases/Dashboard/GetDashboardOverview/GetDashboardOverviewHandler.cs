using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Core.Application.UseCases.Dashboard.Common;
using Core.Application.Interfaces;
using Core.Domain.Entities;
using Domain.Entities;

namespace Core.Application.UseCases.Dashboard.GetDashboardOverview;

public sealed class GetDashboardOverviewHandler(
    ISubscriptionRepository subscriptionRepository,
    ICreditConsumptionRepository creditConsumptionRepository,
    IChatSessionRepository chatSessionRepository,
    IAgentRepository agentRepository,
    IUserPluginRepository userPluginRepository,
    IUserWorkflowRepository userWorkflowRepository,
    IKnowledgeBaseFileRepository knowledgeBaseFileRepository,
    IMediaCenterAssetRepository mediaCenterAssetRepository,
    IMessageRepository messageRepository,
    ILogger<GetDashboardOverviewHandler> logger)
    : IRequestHandler<GetDashboardOverviewQuery, DashboardOverviewDto>
{
    public async Task<DashboardOverviewDto> Handle(GetDashboardOverviewQuery request, CancellationToken cancellationToken)
    {
        var userId = request.UserId;
        
        logger.LogInformation("Starting dashboard overview for user {UserId}", userId);

        try
        {
            // Get all data sequentially to avoid DbContext concurrency issues
            logger.LogDebug("Fetching subscription for user {UserId}", userId);
            var subscription = await subscriptionRepository.GetByUserIdAsync(userId, cancellationToken);
            
            logger.LogDebug("Fetching credit history for user {UserId}", userId);
            var creditHistory = await creditConsumptionRepository.GetRecentByUserIdAsync(userId, 30, cancellationToken);
            
            // var chatSessions = await chatSessionRepository.GetByUserIdAsync(userId, cancellationToken);
            // var agents = await agentRepository.GetByUserIdAsync(userId, cancellationToken);
            
            logger.LogDebug("Fetching user plugins for user {UserId}", userId);
            List<UserPluginEntity> userPlugins = await userPluginRepository.GetByUserIdAsync(userId, cancellationToken);
            
            logger.LogDebug("Fetching user workflows for user {UserId}", userId);
            List<UserWorkflowEntity> userWorkflows = await userWorkflowRepository.GetByUserIdAsync(userId, cancellationToken);
            
            // var knowledgeBaseFiles = await knowledgeBaseFileRepository.GetByUserIdAsync(userId, cancellationToken);
            
            logger.LogDebug("Fetching media assets for user {UserId}", userId);
            var mediaAssets = await mediaCenterAssetRepository.GetByUserIdAsync(userId, cancellationToken);

            logger.LogDebug("Building dashboard metrics for user {UserId}", userId);
            
            return new DashboardOverviewDto
            {
                Credits = await BuildCreditMetrics(subscription, creditHistory, cancellationToken),
                Subscription = BuildSubscriptionMetrics(subscription),
                // Activity = await BuildActivityMetrics(chatSessions, cancellationToken),
                // Agents = BuildAgentMetrics(agents, chatSessions),
                // Usage = BuildUsageStats(userPlugins, userWorkflows, knowledgeBaseFiles, mediaAssets)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error occurred while fetching dashboard overview for user {UserId}", userId);
            throw;
        }
    }

    private static Task<CreditMetricsDto> BuildCreditMetrics(
        SubscriptionEntity? subscription,
        List<CreditConsumptionEntity> creditHistory,
        CancellationToken cancellationToken)
    {
        var currentBalance = subscription?.Credits ?? 0;
        var now = DateTime.UtcNow;
        
        var consumedToday = creditHistory
            .Where(c => c.ConsumedAt.Date == now.Date)
            .Sum(c => c.CreditsConsumed);
            
        var consumedThisWeek = creditHistory
            .Where(c => c.ConsumedAt >= now.AddDays(-7))
            .Sum(c => c.CreditsConsumed);
            
        var consumedThisMonth = creditHistory
            .Where(c => c.ConsumedAt.Month == now.Month && c.ConsumedAt.Year == now.Year)
            .Sum(c => c.CreditsConsumed);

        var last30Days = creditHistory
            .Where(c => c.ConsumedAt >= now.AddDays(-30))
            .ToList();

        var averageDailyConsumption = last30Days.Any() 
            ? (decimal)last30Days.Sum(c => c.CreditsConsumed) / 30 
            : 0;

        var daysUntilExhaustion = averageDailyConsumption > 0 
            ? (int)(currentBalance / averageDailyConsumption) 
            : int.MaxValue;

        var consumptionByType = creditHistory
            .GroupBy(c => c.ConsumptionType)
            .Select(g => new CreditConsumptionByTypeDto
            {
                Type = g.Key,
                CreditsConsumed = g.Sum(c => c.CreditsConsumed),
                Percentage = creditHistory.Any() 
                    ? (decimal)g.Sum(c => c.CreditsConsumed) / creditHistory.Sum(c => c.CreditsConsumed) * 100 
                    : 0
            })
            .OrderByDescending(c => c.CreditsConsumed)
            .ToList();

        var topResources = creditHistory
            .GroupBy(c => new { c.ResourceId, c.ResourceName, c.ConsumptionType })
            .Select(g => new TopResourceConsumptionDto
            {
                // ResourceId = g.Key.ResourceId,
                ResourceName = g.Key.ResourceName,
                ResourceType = g.Key.ConsumptionType,
                CreditsConsumed = g.Sum(c => c.CreditsConsumed)
            })
            .OrderByDescending(r => r.CreditsConsumed)
            .Take(5)
            .ToList();

        var alertThreshold = 100; // TODO: Get from user preferences
        var lowCreditAlert = currentBalance < alertThreshold;

        return Task.FromResult(new CreditMetricsDto
        {
            CurrentBalance = currentBalance,
            ConsumedToday = consumedToday,
            ConsumedThisWeek = consumedThisWeek,
            ConsumedThisMonth = consumedThisMonth,
            AverageDailyConsumption = averageDailyConsumption,
            DaysUntilExhaustion = daysUntilExhaustion,
            ConsumptionByType = consumptionByType,
            TopConsumingResources = topResources,
            LowCreditAlert = lowCreditAlert,
            AlertThreshold = alertThreshold
        });
    }

    private SubscriptionMetricsDto BuildSubscriptionMetrics(SubscriptionEntity? subscription)
    {
        if (subscription == null)
        {
            return new SubscriptionMetricsDto
            {
                PlanName = "Free",
                IncludedCredits = 0,
                ExpiresAt = DateTime.UtcNow,
                DaysUntilExpiration = 0,
                AutoRenew = false,
                Price = 0,
                PaymentUnit = "month",
                RecommendedPlan = "Basic"
            };
        }

        // var daysUntilExpiration = (int)(subscription.ExpiresAt - DateTime.UtcNow).TotalDays;
        
        // TODO: Implement plan recommendation logic based on usage patterns
        var recommendedPlan = subscription.Credits < 500 ? "Pro" : "Basic";

        return new SubscriptionMetricsDto
        {
            PlanName = "Basic", // TODO: Get from subscription plan
            IncludedCredits = subscription.Credits,
            // ExpiresAt = subscription.ExpiresAt,
            // DaysUntilExpiration = Math.Max(0, daysUntilExpiration),
            AutoRenew = subscription.AutoRenew,
            Price = 0, // TODO: Get from subscription plan
            PaymentUnit = "month",
            RecommendedPlan = recommendedPlan
        };
    }

    private async Task<ActivityMetricsDto> BuildActivityMetrics(
        List<ChatSessionEntity> chatSessions,
        CancellationToken cancellationToken)
    {
        var totalSessions = chatSessions.Count;
        var activeSessions = chatSessions.Count(s => !s.IsArchived);
        var archivedSessions = chatSessions.Count(s => s.IsArchived);

        // Get message counts for each session
        var sessionMessageCounts = new Dictionary<Guid, int>();
        foreach (var session in chatSessions)
        {
            var messageCount = await messageRepository.GetCountBySessionIdAsync(session.SessionId, cancellationToken);
            sessionMessageCounts[session.Id] = messageCount;
        }

        var averageMessagesPerSession = sessionMessageCounts.Any() 
            ? (decimal)sessionMessageCounts.Values.Sum() / sessionMessageCounts.Count 
            : 0;

        // Calculate average session duration (based on time between first and last message)
        var sessionDurations = new List<TimeSpan>();
        foreach (var session in chatSessions)
        {
            // if (session.UpdatedAt.HasValue)
            // {
                // var duration = session.UpdatedAt.Value - session.CreatedAt;
                // sessionDurations.Add(duration);
            // }
        }

        var averageSessionDuration = sessionDurations.Any() 
            ? TimeSpan.FromSeconds(sessionDurations.Average(d => d.TotalSeconds)) 
            : TimeSpan.Zero;

        var sessionsByAgent = chatSessions
            .Where(s => s.AgentId.HasValue)
            .GroupBy(s => new { s.AgentId, s.Agent?.Name })
            .Select(g => new SessionsByAgentDto
            {
                AgentId = g.Key.AgentId!.Value,
                AgentName = g.Key.Name ?? "Unknown",
                SessionCount = g.Count(),
                Percentage = totalSessions > 0 ? (decimal)g.Count() / totalSessions * 100 : 0
            })
            .OrderByDescending(s => s.SessionCount)
            .ToList();

        var sessionsByTeam = chatSessions
            .Where(s => s.TeamId.HasValue)
            .GroupBy(s => new { s.TeamId, s.Team?.Name })
            .Select(g => new SessionsByTeamDto
            {
                TeamId = g.Key.TeamId!.Value,
                TeamName = g.Key.Name ?? "Unknown",
                SessionCount = g.Count(),
                Percentage = totalSessions > 0 ? (decimal)g.Count() / totalSessions * 100 : 0
            })
            .OrderByDescending(s => s.SessionCount)
            .ToList();

        // TODO: Get actual activity by hour from message timestamps
        var activityByHour = Enumerable.Range(0, 24)
            .Select(hour => new ActivityByHourDto
            {
                Hour = hour,
                MessageCount = 0 // TODO: Implement actual count
            })
            .ToList();

        return new ActivityMetricsDto
        {
            TotalChatSessions = totalSessions,
            ActiveSessions = activeSessions,
            ArchivedSessions = archivedSessions,
            AverageMessagesPerSession = averageMessagesPerSession,
            AverageSessionDuration = averageSessionDuration,
            SessionsByAgent = sessionsByAgent,
            SessionsByTeam = sessionsByTeam,
            ActivityByHour = activityByHour
        };
    }

    private AgentMetricsDto BuildAgentMetrics(
        List<AgentEntity> agents,
        List<ChatSessionEntity> chatSessions)
    {
        var totalAgents = agents.Count;
        var activeAgents = agents.Count(a => !a.IsDeleted);
        var deletedAgents = agents.Count(a => a.IsDeleted);

        var agentSessionCounts = chatSessions
            .Where(s => s.AgentId.HasValue)
            .GroupBy(s => s.AgentId)
            .ToDictionary(g => g.Key!.Value, g => g.Count());

        var topAgents = agents
            .Where(a => !a.IsDeleted)
            .Select(a => new TopAgentDto
            {
                AgentId = a.Id,
                AgentName = a.Name,
                AvatarUrl = a.AvatarUrl ?? string.Empty,
                SessionCount = agentSessionCounts.GetValueOrDefault(a.Id, 0),
                MessageCount = 0, // TODO: Get actual message count
                AverageResponseTime = 0 // TODO: Calculate actual response time
            })
            .OrderByDescending(a => a.SessionCount)
            .Take(5)
            .ToList();

        var languageDistribution = agents
            .Where(a => !a.IsDeleted && !string.IsNullOrEmpty(a.Language))
            .GroupBy(a => a.Language)
            .Select(g => new AgentLanguageDistributionDto
            {
                // Language = g.Key,
                Count = g.Count(),
                Percentage = activeAgents > 0 ? (decimal)g.Count() / activeAgents * 100 : 0
            })
            .OrderByDescending(l => l.Count)
            .ToList();

        return new AgentMetricsDto
        {
            TotalAgents = totalAgents,
            ActiveAgents = activeAgents,
            DeletedAgents = deletedAgents,
            TopAgents = topAgents,
            LanguageDistribution = languageDistribution
        };
    }

    private UsageStatsDto BuildUsageStats(
        List<UserPluginEntity> userPlugins,
        List<UserWorkflowEntity> userWorkflows,
        List<KnowledgeBaseFileEntity> knowledgeBaseFiles,
        List<MediaCenterAssetEntity> mediaAssets)
    {
        // Plugin usage
        var pluginUsage = new PluginUsageDto
        {
            TotalPurchased = userPlugins.Count,
            ActivePlugins = userPlugins.Count(p => p.Plugin?.IsActive ?? false),
            TopPlugins = userPlugins
                .GroupBy(up => new { up.PluginId, up.Plugin?.Name })
                .Select(g => new TopPluginDto
                {
                    PluginId = g.Key.PluginId,
                    PluginName = g.Key.Name ?? "Unknown",
                    UsageCount = 0, // TODO: Track actual usage
                    CreditsConsumed = 0 // TODO: Track actual consumption
                })
                .Take(5)
                .ToList(),
            SubscriptionBasedCount = userPlugins.Count(p => p.Plugin?.IsSubscriptionBased ?? false),
            OneTimePurchaseCount = userPlugins.Count(p => !(p.Plugin?.IsSubscriptionBased ?? false))
        };

        // Workflow usage
        var workflowUsage = new WorkflowUsageDto
        {
            TotalPurchased = userWorkflows.Count,
            ExecutedThisMonth = 0, // TODO: Track executions
            SuccessRate = 100, // TODO: Track success rate
            TopWorkflows = userWorkflows
                .GroupBy(uw => new { uw.WorkflowId, uw.Workflow?.Name })
                .Select(g => new TopWorkflowDto
                {
                    WorkflowId = g.Key.WorkflowId,
                    WorkflowName = g.Key.Name ?? "Unknown",
                    ExecutionCount = 0, // TODO: Track executions
                    SuccessRate = 100 // TODO: Track success rate
                })
                .Take(5)
                .ToList()
        };

        // Knowledge base usage
        var fileTypeDistribution = knowledgeBaseFiles
            .Where(f => !string.IsNullOrEmpty(f.MimeType))
            .GroupBy(f => f.MimeType)
            .ToDictionary(g => g.Key, g => g.Count());

        var topTags = knowledgeBaseFiles
            // .SelectMany(f => f.Tags ?? new List<string>())
            // .GroupBy(t => t)
            // .OrderByDescending(g => g.Count())
            // .Take(10)
            // .Select(g => g.Key)
            .ToList();

        var knowledgeBaseUsage = new KnowledgeBaseUsageDto
        {
            TotalDocuments = knowledgeBaseFiles.Count,
            TotalSizeBytes = knowledgeBaseFiles.Sum(f => f.FileSize),
            IndexedDocuments = knowledgeBaseFiles.Count, // TODO: Track indexing status
            FileTypeDistribution = fileTypeDistribution,
            // TopTags = topTags
        };

        // Media center usage
        var mediaTypeDistribution = mediaAssets
            .Where(a => !string.IsNullOrEmpty(a.MimeType))
            .GroupBy(a => a.MimeType)
            .ToDictionary(g => g.Key, g => g.Count());

        // var totalDuration = TimeSpan.FromSeconds(
            // mediaAssets.Where(a => a.Duration.HasValue).Sum(a => a.Duration!.Value)
        // );

        var mediaCenterUsage = new MediaCenterUsageDto
        {
            TotalAssets = mediaAssets.Count,
            TotalSizeBytes = 0, // TODO: Track file sizes
            // TotalDuration = totalDuration,
            MediaTypeDistribution = mediaTypeDistribution
        };

        return new UsageStatsDto
        {
            Plugins = pluginUsage,
            Workflows = workflowUsage,
            KnowledgeBase = knowledgeBaseUsage,
            MediaCenter = mediaCenterUsage
        };
    }
}