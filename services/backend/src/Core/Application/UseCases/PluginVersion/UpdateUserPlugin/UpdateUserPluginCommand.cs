using MediatR;

namespace Application.UseCases.PluginVersion.UpdateUserPlugin;

public sealed record UpdateUserPluginCommand(
    Guid UserId,
    Guid PluginId,
    string? TargetVersion = null
) : IRequest<UpdateUserPluginResultDto>;

public sealed record UpdateUserPluginResultDto(
    bool Success,
    string? PreviousVersion,
    string? NewVersion,
    string? ErrorMessage = null
);
