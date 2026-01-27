using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.DeleteOpenApiPlugin;

public sealed class DeleteOpenApiPluginHandler(
    IPluginRepository pluginRepository,
    ILogger<DeleteOpenApiPluginHandler> logger) : IRequestHandler<DeleteOpenApiPluginCommand, Unit>
{
    public async Task<Unit> Handle(
        DeleteOpenApiPluginCommand request,
        CancellationToken cancellationToken)
    {
        var plugin = await pluginRepository.GetByIdAsync(request.Id, cancellationToken);

        if (plugin is null)
            throw new PluginNotFoundException(request.Id);

        if (plugin.PluginType != "openapi")
            throw new ValidationException(
                $"Plugin {request.Id} is not an OpenAPI plugin.",
                nameof(request.Id));

        await pluginRepository.DeleteAsync(request.Id, cancellationToken);

        logger.LogInformation(
            "Deleted OpenAPI plugin: {PluginName} (Id: {PluginId})",
            plugin.Name,
            plugin.Id);

        return Unit.Value;
    }
}
