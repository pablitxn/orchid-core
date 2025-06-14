using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Core.Application.UseCases.Dashboard.GetDashboardOverview;
using Core.Application.UseCases.Dashboard.GetCreditHistory;
using Core.Application.UseCases.Dashboard.GetSessionDetails;
using Core.Application.UseCases.Dashboard.GetSecurityMetrics;
using System.Security.Claims;

namespace WebApi.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public DashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in claims");
        }
        return userId;
    }

    /// <summary>
    /// Get comprehensive dashboard overview with all metrics
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetDashboardOverview()
    {
        var userId = GetUserId();
        var query = new GetDashboardOverviewQuery(userId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get detailed credit consumption history with filtering and pagination
    /// </summary>
    [HttpGet("credits/history")]
    public async Task<IActionResult> GetCreditHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? consumptionType = null)
    {
        var userId = GetUserId();
        var query = new GetCreditHistoryQuery(userId, pageNumber, pageSize, startDate, endDate, consumptionType);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get credit consumption statistics by type for a date range
    /// </summary>
    [HttpGet("credits/statistics")]
    public async Task<IActionResult> GetCreditStatistics(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        // TODO: Implement GetCreditStatisticsQuery
        return Ok(new { message = "Credit statistics endpoint - implementation pending" });
    }

    /// <summary>
    /// Get detailed session information with filtering and pagination
    /// </summary>
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessionDetails(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isArchived = null,
        [FromQuery] Guid? agentId = null,
        [FromQuery] Guid? teamId = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        var query = new GetSessionDetailsQuery(userId, pageNumber, pageSize, isArchived, agentId, teamId, startDate, endDate);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get session activity heatmap data
    /// </summary>
    [HttpGet("sessions/activity-heatmap")]
    public async Task<IActionResult> GetSessionActivityHeatmap(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        // TODO: Implement GetSessionActivityHeatmapQuery
        return Ok(new { message = "Session activity heatmap endpoint - implementation pending" });
    }

    /// <summary>
    /// Get agent performance metrics
    /// </summary>
    [HttpGet("agents/performance")]
    public async Task<IActionResult> GetAgentPerformance(
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        // TODO: Implement GetAgentPerformanceQuery
        return Ok(new { message = "Agent performance endpoint - implementation pending" });
    }

    /// <summary>
    /// Get plugin usage analytics
    /// </summary>
    [HttpGet("plugins/analytics")]
    public async Task<IActionResult> GetPluginAnalytics()
    {
        var userId = GetUserId();
        // TODO: Implement GetPluginAnalyticsQuery
        return Ok(new { message = "Plugin analytics endpoint - implementation pending" });
    }

    /// <summary>
    /// Get workflow execution analytics
    /// </summary>
    [HttpGet("workflows/analytics")]
    public async Task<IActionResult> GetWorkflowAnalytics()
    {
        var userId = GetUserId();
        // TODO: Implement GetWorkflowAnalyticsQuery
        return Ok(new { message = "Workflow analytics endpoint - implementation pending" });
    }

    /// <summary>
    /// Get knowledge base usage statistics
    /// </summary>
    [HttpGet("knowledge-base/statistics")]
    public async Task<IActionResult> GetKnowledgeBaseStatistics()
    {
        var userId = GetUserId();
        // TODO: Implement GetKnowledgeBaseStatisticsQuery
        return Ok(new { message = "Knowledge base statistics endpoint - implementation pending" });
    }

    /// <summary>
    /// Get media center usage statistics
    /// </summary>
    [HttpGet("media-center/statistics")]
    public async Task<IActionResult> GetMediaCenterStatistics()
    {
        var userId = GetUserId();
        // TODO: Implement GetMediaCenterStatisticsQuery
        return Ok(new { message = "Media center statistics endpoint - implementation pending" });
    }

    /// <summary>
    /// Get user security metrics and login history
    /// </summary>
    [HttpGet("security")]
    public async Task<IActionResult> GetSecurityMetrics()
    {
        var userId = GetUserId();
        var query = new GetSecurityMetricsQuery(userId);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Get billing history
    /// </summary>
    [HttpGet("billing/history")]
    public async Task<IActionResult> GetBillingHistory(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        // TODO: Implement GetBillingHistoryQuery
        return Ok(new { message = "Billing history endpoint - implementation pending" });
    }

    /// <summary>
    /// Get usage forecast based on historical data
    /// </summary>
    [HttpGet("forecast")]
    public async Task<IActionResult> GetUsageForecast()
    {
        var userId = GetUserId();
        // TODO: Implement GetUsageForecastQuery
        return Ok(new { message = "Usage forecast endpoint - implementation pending" });
    }

    /// <summary>
    /// Export dashboard data as CSV or PDF
    /// </summary>
    [HttpGet("export")]
    public async Task<IActionResult> ExportDashboardData(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null)
    {
        var userId = GetUserId();
        // TODO: Implement ExportDashboardDataQuery
        return Ok(new { message = "Export dashboard data endpoint - implementation pending" });
    }
}