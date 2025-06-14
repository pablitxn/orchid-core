using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using WebApi.Hubs;

namespace WebApi.Controllers;

/// <summary>
///     Provides access to the history of backend activities stored in Redis.
/// </summary>
[ApiController]
[Route("api/activities")]
public class ActivitiesController(IDistributedCache cache) : ControllerBase
{
    private const string HistoryKey = "activity_history";

    /// <summary>
    ///     Returns the full list of activities stored in Redis.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActivities()
    {
        var json = await cache.GetStringAsync(HistoryKey);

        if (string.IsNullOrEmpty(json)) return Ok(Array.Empty<Activity>());

        var activities = JsonSerializer.Deserialize<List<Activity>>(json) ?? new List<Activity>();
        return Ok(activities);
    }
}