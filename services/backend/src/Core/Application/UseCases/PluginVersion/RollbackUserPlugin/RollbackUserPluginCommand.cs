using MediatR;

namespace Application.UseCases.PluginVersion.RollbackUserPlugin;

public sealed record RollbackUserPluginCommand(
    Guid UserId,
    Guid PluginId,
    string TargetVersion
) : IRequest<RollbackUserPluginResultDto>;

public sealed record RollbackUserPluginResultDto(
    bool Success,
    string? PreviousVersion,
    string? NewVersion,
    string? ErrorMessage = null
);
