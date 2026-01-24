using Application.UseCases.Plugin.Common;
using MediatR;

namespace Application.UseCases.Plugin.ListPlugins;

public record ListPluginsQuery : IRequest<List<PluginDto>>;
