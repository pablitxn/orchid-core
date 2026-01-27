using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.UpdateOpenApiPlugin;

/// <summary>
/// Command to update an existing OpenAPI plugin.
/// Note: SystemName cannot be updated after creation.
/// </summary>
public sealed record UpdateOpenApiPluginCommand(
    Guid Id,
    string? Name = null,
    string? Description = null,
    string? BaseUrl = null,
    string? OpenApiSpecUrl = null,
    int? TimeoutSeconds = null,
    int? PriceCredits = null,
    bool? IsSubscriptionBased = null,
    bool? IsActive = null) : IRequest<OpenApiPluginDto>;
