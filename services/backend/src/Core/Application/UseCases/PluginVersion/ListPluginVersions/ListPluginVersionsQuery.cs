using Application.UseCases.PluginVersion.Common;
using MediatR;

namespace Application.UseCases.PluginVersion.ListPluginVersions;

public sealed record ListPluginVersionsQuery(Guid PluginId) : IRequest<List<PluginVersionDto>>;
