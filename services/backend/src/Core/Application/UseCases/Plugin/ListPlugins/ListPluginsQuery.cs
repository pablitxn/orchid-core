using Domain.Entities;
using MediatR;

namespace Application.UseCases.Plugin.ListPlugins;

public record ListPluginsQuery : IRequest<List<PluginEntity>>;