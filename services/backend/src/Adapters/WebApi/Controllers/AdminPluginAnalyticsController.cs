using Application.UseCases.PluginAnalytics.GetPlatformPluginStats;
using Application.UseCases.PluginAnalytics.GetPluginUsageStats;
using Domain.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/admin/plugin-analytics")]
[Authorize(Roles = "admin")]
public class AdminPluginAnalyticsController(IMediator mediator, ILogger<AdminPluginAnalyticsController> logger) : ControllerBase
{
    /// <summary>
    /// Get platform-wide plugin analytics
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetPlatformStats(
        [FromQuery] string period = "last_30_days",
        [FromQuery] int topN = 10,
        CancellationToken ct = default)
    {
        try
        {
            var query = new GetPlatformPluginStatsQuery(period, topN);
            var result = await mediator.Send(query, ct);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting platform plugin stats");
            return StatusCode(500, new { error = "An error occurred while fetching analytics" });
        }
    }

    /// <summary>
    /// Get detailed analytics for any plugin (admin access - no ownership check)
    /// </summary>
    [HttpGet("{pluginId:guid}")]
    public async Task<IActionResult> GetPluginStats(
        Guid pluginId,
        [FromQuery] string period = "last_30_days",
        CancellationToken ct = default)
    {
        try
        {
            // Admin version: use a special query or bypass ownership check
            // For now, we'll create a dedicated admin query variant
            var query = new GetPluginUsageStatsAdminQuery(pluginId, period);
            var result = await mediator.Send(query, ct);
            return Ok(result);
        }
        catch (PluginNotFoundException)
        {
            return NotFound(new { error = "Plugin not found" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting plugin analytics for {PluginId}", pluginId);
            return StatusCode(500, new { error = "An error occurred while fetching analytics" });
        }
    }
}
