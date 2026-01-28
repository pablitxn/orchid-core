using MediatR;

namespace Application.UseCases.UserPlugin.UninstallPlugin;

public sealed record UninstallPluginCommand(Guid UserId, Guid PluginId) : IRequest<UninstallPluginDto>;

public sealed record UninstallPluginDto(bool Success, string? ErrorMessage = null);
