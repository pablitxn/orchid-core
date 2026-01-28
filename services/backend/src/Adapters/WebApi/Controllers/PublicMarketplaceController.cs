using Application.UseCases.Plugin.GetPublicPluginDetails;
using Application.UseCases.Plugin.ListFeaturedPlugins;
using Application.UseCases.Plugin.ListPluginCategories;
using Application.UseCases.Plugin.ListPublicPlugins;
using Application.UseCases.Workflow.ListWorkflows;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/public/marketplace")]
[AllowAnonymous]
public class PublicMarketplaceController(IMediator mediator) : ControllerBase
{
    [HttpGet("plugins")]
    public async Task<IActionResult> ListPlugins(
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] decimal? minRating = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] string? compatibility = null,
        [FromQuery] PluginSortBy sortBy = PluginSortBy.Rating,
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new ListPublicPluginsQuery
        {
            SearchTerm = search,
            Category = category,
            MinRating = minRating,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            Compatibility = compatibility,
            SortBy = sortBy,
            SortDescending = sortDescending,
            Page = page,
            PageSize = pageSize
        };

        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("plugins/featured")]
    public async Task<IActionResult> ListFeaturedPlugins(
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new ListFeaturedPluginsQuery(limit);
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("plugins/categories")]
    public async Task<IActionResult> ListPluginCategories(CancellationToken cancellationToken)
    {
        var query = new ListPluginCategoriesQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("plugins/{pluginId}")]
    public async Task<IActionResult> GetPluginDetails(Guid pluginId, CancellationToken cancellationToken)
    {
        var query = new GetPublicPluginDetailsQuery(pluginId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpGet("workflows")]
    public async Task<IActionResult> ListWorkflows(CancellationToken cancellationToken)
    {
        var query = new ListPublicWorkflowsQuery();
        var result = await mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workflows/{workflowId}")]
    public async Task<IActionResult> GetWorkflowDetails(Guid workflowId, CancellationToken cancellationToken)
    {
        var query = new GetPublicWorkflowDetailsQuery(workflowId);
        var result = await mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }
}
