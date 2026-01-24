using MediatR;

namespace Application.UseCases.Plugin.ListPublicPlugins;

public sealed record ListPublicPluginsQuery : IRequest<ListPublicPluginsDto>;