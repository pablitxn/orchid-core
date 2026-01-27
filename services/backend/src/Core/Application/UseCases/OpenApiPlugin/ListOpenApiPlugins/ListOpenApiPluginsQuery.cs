using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.ListOpenApiPlugins;

/// <summary>
/// Query to list all OpenAPI plugins.
/// </summary>
public sealed record ListOpenApiPluginsQuery : IRequest<IReadOnlyList<OpenApiPluginDto>>;
