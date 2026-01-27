using Application.UseCases.OpenApiPlugin.Common;
using MediatR;

namespace Application.UseCases.OpenApiPlugin.GetOpenApiPlugin;

/// <summary>
/// Query to get a single OpenAPI plugin by ID, including its available functions.
/// </summary>
public sealed record GetOpenApiPluginQuery(Guid Id) : IRequest<OpenApiPluginDto>;
