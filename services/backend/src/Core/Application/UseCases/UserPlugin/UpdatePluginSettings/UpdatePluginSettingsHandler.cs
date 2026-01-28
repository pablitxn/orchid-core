using System.Text.Json;
using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.UserPlugin.UpdatePluginSettings;

public sealed class UpdatePluginSettingsHandler(
    IUserPluginRepository userPluginRepository,
    ILogger<UpdatePluginSettingsHandler> logger
) : IRequestHandler<UpdatePluginSettingsCommand, UpdatePluginSettingsDto>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly ILogger<UpdatePluginSettingsHandler> _logger = logger;

    public async Task<UpdatePluginSettingsDto> Handle(UpdatePluginSettingsCommand command, CancellationToken cancellationToken)
    {
        var userPlugin = await _userPluginRepository.GetByUserAndPluginAsync(
            command.UserId, command.PluginId, cancellationToken);

        if (userPlugin == null)
        {
            return new UpdatePluginSettingsDto(false, "Plugin not found or not installed");
        }

        if (!userPlugin.IsActive)
        {
            return new UpdatePluginSettingsDto(false, "Plugin is not active. Please reinstall the plugin first.");
        }

        try
        {
            // Serialize settings to JSON
            userPlugin.Settings = command.Settings != null
                ? JsonSerializer.Serialize(command.Settings)
                : null;

            await _userPluginRepository.UpdateAsync(userPlugin, cancellationToken);

            _logger.LogInformation("User {UserId} updated settings for plugin {PluginId}",
                command.UserId, command.PluginId);

            return new UpdatePluginSettingsDto(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating settings for plugin {PluginId} for user {UserId}",
                command.PluginId, command.UserId);
            return new UpdatePluginSettingsDto(false, "An error occurred while updating plugin settings");
        }
    }
}
