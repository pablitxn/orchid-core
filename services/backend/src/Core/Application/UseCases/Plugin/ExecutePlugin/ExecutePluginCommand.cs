using MediatR;

namespace Application.UseCases.Plugin.ExecutePlugin;

public sealed record ExecutePluginCommand(
    Guid UserId,
    Guid PluginId,
    string Parameters
) : IRequest<ExecutePluginResult>;