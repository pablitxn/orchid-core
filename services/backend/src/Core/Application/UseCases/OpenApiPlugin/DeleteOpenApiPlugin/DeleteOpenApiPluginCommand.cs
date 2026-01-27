using MediatR;

namespace Application.UseCases.OpenApiPlugin.DeleteOpenApiPlugin;

/// <summary>
/// Command to delete an OpenAPI plugin.
/// </summary>
public sealed record DeleteOpenApiPluginCommand(Guid Id) : IRequest<Unit>;
