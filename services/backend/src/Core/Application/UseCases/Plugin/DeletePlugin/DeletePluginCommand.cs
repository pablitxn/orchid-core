using MediatR;

namespace Application.UseCases.Plugin.DeletePlugin;

public sealed record DeletePluginCommand(Guid PluginId, Guid UserId) : IRequest<Unit>;