using Application.UseCases.UserPlugin.Common;
using MediatR;

namespace Application.UseCases.UserPlugin.ListUserPlugins;

public sealed record ListUserPluginsQuery(Guid UserId) : IRequest<List<UserPluginDto>>;
