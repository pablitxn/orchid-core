using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.UpdateOpenApiPlugin;

public sealed class UpdateOpenApiPluginHandler(
    IPluginRepository pluginRepository,
    ILogger<UpdateOpenApiPluginHandler> logger) : IRequestHandler<UpdateOpenApiPluginCommand, OpenApiPluginDto>
{
    public async Task<OpenApiPluginDto> Handle(
        UpdateOpenApiPluginCommand request,
        CancellationToken cancellationToken)
    {
        OpenApiPluginValidator.ValidateUpdate(
            request.Name,
            request.BaseUrl,
            request.OpenApiSpecUrl,
            request.TimeoutSeconds,
            request.Description);

        var plugin = await pluginRepository.GetByIdAsync(request.Id, cancellationToken);

        if (plugin is null)
            throw new PluginNotFoundException(request.Id);

        if (plugin.PluginType != "openapi")
            throw new ValidationException(
                $"Plugin {request.Id} is not an OpenAPI plugin.",
                nameof(request.Id));

        if (request.Name is not null)
            plugin.Name = request.Name;

        if (request.Description is not null)
            plugin.Description = request.Description;

        if (request.BaseUrl is not null)
        {
            if (plugin.BaseUrl != request.BaseUrl)
            {
                plugin.OpenApiSpecCache = null;
                plugin.SpecCachedAt = null;
            }
            plugin.BaseUrl = request.BaseUrl;
        }

        if (request.OpenApiSpecUrl is not null)
        {
            if (plugin.OpenApiSpecUrl != request.OpenApiSpecUrl)
            {
                plugin.OpenApiSpecCache = null;
                plugin.SpecCachedAt = null;
            }
            plugin.OpenApiSpecUrl = request.OpenApiSpecUrl;
        }

        if (request.TimeoutSeconds is not null)
            plugin.TimeoutSeconds = request.TimeoutSeconds;

        if (request.PriceCredits is not null)
            plugin.PriceCredits = request.PriceCredits.Value;

        if (request.IsSubscriptionBased is not null)
            plugin.IsSubscriptionBased = request.IsSubscriptionBased.Value;

        if (request.IsActive is not null)
            plugin.IsActive = request.IsActive.Value;

        plugin.UpdatedAt = DateTime.UtcNow;

        await pluginRepository.UpdateAsync(plugin, cancellationToken);

        logger.LogInformation(
            "Updated OpenAPI plugin: {PluginName} (Id: {PluginId})",
            plugin.Name,
            plugin.Id);

        return OpenApiPluginDto.FromEntity(plugin);
    }
}
