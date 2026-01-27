using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;

/// <summary>
/// Command to create a new OpenAPI plugin.
/// </summary>
public sealed record CreateOpenApiPluginCommand(
    string Name,
    string SystemName,
    string BaseUrl,
    string? Description = null,
    string? OpenApiSpecUrl = null,
    int? TimeoutSeconds = null,
    int PriceCredits = 10,
    bool IsSubscriptionBased = false,
    bool IsActive = false) : IRequest<OpenApiPluginDto>;
