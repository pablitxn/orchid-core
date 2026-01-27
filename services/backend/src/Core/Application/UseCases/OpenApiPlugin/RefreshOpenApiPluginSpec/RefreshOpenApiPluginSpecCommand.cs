using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.RefreshOpenApiPluginSpec;

/// <summary>
/// Command to force refresh the OpenAPI spec cache for a plugin.
/// </summary>
public sealed record RefreshOpenApiPluginSpecCommand(Guid Id) : IRequest<OpenApiPluginDto>;
