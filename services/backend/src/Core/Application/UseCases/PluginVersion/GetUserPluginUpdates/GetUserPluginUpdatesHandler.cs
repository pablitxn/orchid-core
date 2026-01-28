using Application.Interfaces;
using Application.UseCases.PluginVersion.Common;
using Domain.ValueObjects;
using MediatR;

namespace Application.UseCases.PluginVersion.GetUserPluginUpdates;

public sealed class GetUserPluginUpdatesHandler(
    IUserPluginRepository userPluginRepository,
    IPluginVersionRepository pluginVersionRepository
) : IRequestHandler<GetUserPluginUpdatesQuery, List<PluginUpdateAvailableDto>>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IPluginVersionRepository _pluginVersionRepository = pluginVersionRepository;

    public async Task<List<PluginUpdateAvailableDto>> Handle(GetUserPluginUpdatesQuery request, CancellationToken cancellationToken)
    {
        var userPlugins = await _userPluginRepository.ListByUserAsync(request.UserId, cancellationToken);
        var updates = new List<PluginUpdateAvailableDto>();

        foreach (var userPlugin in userPlugins.Where(up => up.IsActive))
        {
            // Skip plugins without installed version info
            if (string.IsNullOrEmpty(userPlugin.InstalledVersion))
            {
                continue;
            }

            // Skip pinned versions
            if (!string.IsNullOrEmpty(userPlugin.PinnedVersion))
            {
                continue;
            }

            var latestVersion = await _pluginVersionRepository.GetLatestAsync(
                userPlugin.PluginId,
                includePreRelease: false,
                cancellationToken);

            if (latestVersion is null)
            {
                continue;
            }

            // Compare versions
            if (!SemanticVersion.TryParse(userPlugin.InstalledVersion, out var installedSemVer) ||
                !SemanticVersion.TryParse(latestVersion.Version, out var latestSemVer))
            {
                continue;
            }

            // Check if update is available
            if (latestSemVer!.IsNewerThan(installedSemVer!))
            {
                updates.Add(new PluginUpdateAvailableDto
                {
                    PluginId = userPlugin.PluginId,
                    PluginName = userPlugin.Plugin.Name,
                    PluginIcon = userPlugin.Plugin.Icon,
                    InstalledVersion = userPlugin.InstalledVersion,
                    LatestVersion = latestVersion.Version,
                    Changelog = latestVersion.Changelog,
                    IsBreakingChange = latestVersion.IsBreakingChange,
                    IsDeprecated = latestVersion.IsDeprecated,
                    DeprecationEndsAt = latestVersion.DeprecationEndsAt,
                    DeprecationMessage = latestVersion.DeprecationMessage
                });
            }
        }

        return updates;
    }
}
