using Application.UseCases.PluginVersion.Common;
using MediatR;

namespace Application.UseCases.PluginVersion.GetUserPluginUpdates;

public sealed record GetUserPluginUpdatesQuery(Guid UserId) : IRequest<List<PluginUpdateAvailableDto>>;
