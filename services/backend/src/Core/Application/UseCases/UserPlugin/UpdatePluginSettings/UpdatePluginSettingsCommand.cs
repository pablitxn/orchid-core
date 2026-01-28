using MediatR;

namespace Application.UseCases.UserPlugin.UpdatePluginSettings;

public sealed record UpdatePluginSettingsCommand(
    Guid UserId,
    Guid PluginId,
    Dictionary<string, object>? Settings
) : IRequest<UpdatePluginSettingsDto>;

public sealed record UpdatePluginSettingsDto(bool Success, string? ErrorMessage = null);
