using Application.UseCases.Plugin.Common;
using MediatR;

namespace Application.UseCases.Agent.GetAgentPlugins;

public sealed record GetAgentPluginsQuery(Guid AgentId, Guid UserId) : IRequest<GetAgentPluginsDto>;

public sealed record GetAgentPluginsDto(bool Success, List<PluginDto> Plugins, string? ErrorMessage = null);
