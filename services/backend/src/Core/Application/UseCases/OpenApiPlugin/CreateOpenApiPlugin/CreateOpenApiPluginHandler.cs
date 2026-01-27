using Application.Interfaces;
using Application.UseCases.OpenApiPlugin.Common;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.OpenApiPlugin.CreateOpenApiPlugin;

public sealed class CreateOpenApiPluginHandler(
    IPluginRepository pluginRepository,
    ILogger<CreateOpenApiPluginHandler> logger) : IRequestHandler<CreateOpenApiPluginCommand, OpenApiPluginDto>
{
    public async Task<OpenApiPluginDto> Handle(
        CreateOpenApiPluginCommand request,
        CancellationToken cancellationToken)
    {
        OpenApiPluginValidator.ValidateCreate(
            request.Name,
            request.SystemName,
            request.BaseUrl,
            request.OpenApiSpecUrl,
            request.TimeoutSeconds,
            request.Description);

        var existing = await pluginRepository.GetBySystemNameAsync(
            request.SystemName, cancellationToken);

        if (existing is not null)
        {
            throw new ValidationException(
                $"A plugin with SystemName '{request.SystemName}' already exists.",
                nameof(request.SystemName));
        }

        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            SystemName = request.SystemName,
            PluginType = "openapi",
            BaseUrl = request.BaseUrl,
            OpenApiSpecUrl = request.OpenApiSpecUrl ?? "/openapi.json",
            TimeoutSeconds = request.TimeoutSeconds ?? 30,
            PriceCredits = request.PriceCredits,
            IsSubscriptionBased = request.IsSubscriptionBased,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await pluginRepository.CreateAsync(plugin, cancellationToken);

        logger.LogInformation(
            "Created OpenAPI plugin: {PluginName} (Id: {PluginId}, SystemName: {SystemName})",
            plugin.Name,
            plugin.Id,
            plugin.SystemName);

        return OpenApiPluginDto.FromEntity(plugin);
    }
}
