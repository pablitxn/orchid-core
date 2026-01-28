using Application.UseCases.Publisher.Common;
using MediatR;

namespace Application.UseCases.Publisher.GetPluginStats;

public record GetPluginStatsQuery(Guid UserId, Guid PluginId) : IRequest<PluginStatsDto>;
