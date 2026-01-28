using Application.Interfaces;
using Domain.Events;
using Domain.Exceptions;
using Domain.ValueObjects;
using MediatR;

namespace Application.UseCases.PluginVersion.UpdateUserPlugin;

public sealed class UpdateUserPluginHandler(
    IUserPluginRepository userPluginRepository,
    IPluginVersionRepository pluginVersionRepository,
    IPublisher publisher
) : IRequestHandler<UpdateUserPluginCommand, UpdateUserPluginResultDto>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IPluginVersionRepository _pluginVersionRepository = pluginVersionRepository;
    private readonly IPublisher _publisher = publisher;

    public async Task<UpdateUserPluginResultDto> Handle(UpdateUserPluginCommand request, CancellationToken cancellationToken)
    {
        // Get user's plugin
        var userPlugin = await _userPluginRepository.GetByUserAndPluginAsync(
            request.UserId, request.PluginId, cancellationToken);

        if (userPlugin is null || !userPlugin.IsActive)
        {
            return new UpdateUserPluginResultDto(false, null, null, "Plugin not found or not installed");
        }

        var previousVersion = userPlugin.InstalledVersion ?? "unknown";

        // Get target version (specified or latest)
        Domain.Entities.PluginVersionEntity? targetVersionEntity;
        if (!string.IsNullOrEmpty(request.TargetVersion))
        {
            targetVersionEntity = await _pluginVersionRepository.GetByPluginAndVersionAsync(
                request.PluginId, request.TargetVersion, cancellationToken);

            if (targetVersionEntity is null)
            {
                throw new PluginVersionNotFoundException(request.PluginId, request.TargetVersion);
            }
        }
        else
        {
            targetVersionEntity = await _pluginVersionRepository.GetLatestAsync(
                request.PluginId, includePreRelease: false, cancellationToken);

            if (targetVersionEntity is null)
            {
                return new UpdateUserPluginResultDto(false, previousVersion, null, "No versions available for this plugin");
            }
        }

        // Validate target version is active
        if (!targetVersionEntity.IsActive)
        {
            return new UpdateUserPluginResultDto(false, previousVersion, null, "Target version is not active");
        }

        // Compare versions - ensure we're actually updating
        if (!string.IsNullOrEmpty(userPlugin.InstalledVersion) &&
            SemanticVersion.TryParse(userPlugin.InstalledVersion, out var currentSemVer) &&
            SemanticVersion.TryParse(targetVersionEntity.Version, out var targetSemVer))
        {
            if (!targetSemVer!.IsNewerThan(currentSemVer!))
            {
                return new UpdateUserPluginResultDto(false, previousVersion, targetVersionEntity.Version,
                    "Target version is not newer than current version");
            }

            // Check compatibility (same major version unless it's an upgrade from initial version)
            if (currentSemVer.Major != 0 && !targetSemVer.IsCompatibleWith(currentSemVer))
            {
                // Allow breaking updates with warning
                if (targetVersionEntity.IsBreakingChange)
                {
                    // Log warning but continue - breaking change is expected
                }
            }
        }

        // Check deprecation warning
        if (targetVersionEntity.IsDeprecated)
        {
            // We allow updating to deprecated versions but log a warning
            // The frontend should display the deprecation message
        }

        // Update user plugin
        userPlugin.InstalledVersionId = targetVersionEntity.Id;
        userPlugin.InstalledVersion = targetVersionEntity.Version;
        userPlugin.LastUpdatedAt = DateTime.UtcNow;

        await _userPluginRepository.UpdateAsync(userPlugin, cancellationToken);

        // Publish event
        await _publisher.Publish(new UserPluginUpdatedEvent(
            request.UserId,
            request.PluginId,
            previousVersion,
            targetVersionEntity.Version,
            WasRollback: false
        ), cancellationToken);

        return new UpdateUserPluginResultDto(true, previousVersion, targetVersionEntity.Version);
    }
}
