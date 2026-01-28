using Application.UseCases.UserPlugin.Common;
using MediatR;

namespace Application.UseCases.UserPlugin.GetUserPlugin;

public sealed record GetUserPluginQuery(Guid UserId, Guid PluginId) : IRequest<UserPluginDto?>;
