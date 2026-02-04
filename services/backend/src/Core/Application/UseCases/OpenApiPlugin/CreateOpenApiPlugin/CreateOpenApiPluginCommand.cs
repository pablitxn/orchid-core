using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;

/// <summary>
/// Command to create a new OpenAPI plugin.
/// </summary>
/// <param name="CreatorUserId">The ID of the admin user creating the plugin. They will be granted automatic access.</param>
public sealed record CreateOpenApiPluginCommand(
    Guid CreatorUserId,
    string Name,
    string SystemName,
    string BaseUrl,
    string? Description = null,
    string? OpenApiSpecUrl = null,
    int? TimeoutSeconds = null,
    int PriceCredits = 10,
    bool IsSubscriptionBased = false,
    bool IsActive = false) : IRequest<OpenApiPluginDto>;
