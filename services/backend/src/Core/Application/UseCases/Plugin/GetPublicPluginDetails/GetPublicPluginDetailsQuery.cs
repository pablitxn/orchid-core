using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record GetPublicPluginDetailsQuery(Guid PluginId) : IRequest<PublicPluginDetails?>;