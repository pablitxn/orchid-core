using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record ListPublicPluginsQuery : IRequest<ListPublicPluginsResult>;