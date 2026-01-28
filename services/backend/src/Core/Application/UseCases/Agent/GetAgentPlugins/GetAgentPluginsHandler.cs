using Application.Interfaces;
using Application.UseCases.Plugin.Common;
using MediatR;

namespace Application.UseCases.Agent.GetAgentPlugins;

public sealed class GetAgentPluginsHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository
) : IRequestHandler<GetAgentPluginsQuery, GetAgentPluginsDto>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<GetAgentPluginsDto> Handle(GetAgentPluginsQuery request, CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);

        if (agent == null)
        {
            return new GetAgentPluginsDto(false, new List<PluginDto>(), "Agent not found");
        }

        // Check access: user must be owner or agent must be public
        if (!agent.IsPublic && agent.UserId != request.UserId)
        {
            return new GetAgentPluginsDto(false, new List<PluginDto>(), "Access denied");
        }

        if (agent.PluginIds == null || agent.PluginIds.Length == 0)
        {
            return new GetAgentPluginsDto(true, new List<PluginDto>());
        }

        // Get all plugins and filter to ones assigned to this agent
        var allPlugins = await _pluginRepository.ListAsync(cancellationToken);
        var agentPlugins = allPlugins
            .Where(p => agent.PluginIds.Contains(p.Id))
            .Select(PluginDto.FromEntity)
            .ToList();

        return new GetAgentPluginsDto(true, agentPlugins);
    }
}
