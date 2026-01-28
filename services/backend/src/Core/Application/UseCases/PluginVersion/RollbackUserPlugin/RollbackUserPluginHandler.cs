using Application.Interfaces;
using Domain.Events;
using Domain.Exceptions;
using Domain.ValueObjects;
using MediatR;

namespace Application.UseCases.PluginVersion.RollbackUserPlugin;

public sealed class RollbackUserPluginHandler(
    IUserPluginRepository userPluginRepository,
    IPluginVersionRepository pluginVersionRepository,
    IPublisher publisher
) : IRequestHandler<RollbackUserPluginCommand, RollbackUserPluginResultDto>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IPluginVersionRepository _pluginVersionRepository = pluginVersionRepository;
    private readonly IPublisher _publisher = publisher;

    public async Task<RollbackUserPluginResultDto> Handle(RollbackUserPluginCommand request, CancellationToken cancellationToken)
    {
        // Get user's plugin
        var userPlugin = await _userPluginRepository.GetByUserAndPluginAsync(
            request.UserId, request.PluginId, cancellationToken);

        if (userPlugin is null || !userPlugin.IsActive)
        {
            return new RollbackUserPluginResultDto(false, null, null, "Plugin not found or not installed");
        }

        var previousVersion = userPlugin.InstalledVersion ?? "unknown";

        // Get target version
        var targetVersionEntity = await _pluginVersionRepository.GetByPluginAndVersionAsync(
            request.PluginId, request.TargetVersion, cancellationToken);

        if (targetVersionEntity is null)
        {
            throw new PluginVersionNotFoundException(request.PluginId, request.TargetVersion);
        }

        // Validate target version is active
        if (!targetVersionEntity.IsActive)
        {
            return new RollbackUserPluginResultDto(false, previousVersion, null, "Target version is not active");
        }

        // Validate this is actually a rollback (target is older)
        if (!string.IsNullOrEmpty(userPlugin.InstalledVersion) &&
            SemanticVersion.TryParse(userPlugin.InstalledVersion, out var currentSemVer) &&
            SemanticVersion.TryParse(targetVersionEntity.Version, out var targetSemVer))
        {
            if (!targetSemVer!.IsOlderThan(currentSemVer!))
            {
                return new RollbackUserPluginResultDto(false, previousVersion, request.TargetVersion,
                    "Target version is not older than current version. Use update instead.");
            }

            // Check compatibility (must be same major version for rollback)
            if (!targetSemVer.IsCompatibleWith(currentSemVer))
            {
                throw new IncompatiblePluginVersionException(previousVersion, request.TargetVersion);
            }
        }

        // Warn if rolling back to deprecated version
        if (targetVersionEntity.IsDeprecated)
        {
            // We allow rollback to deprecated versions but frontend should warn
            // The response can include deprecation info via the entity
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
            WasRollback: true
        ), cancellationToken);

        return new RollbackUserPluginResultDto(true, previousVersion, targetVersionEntity.Version);
    }
}
