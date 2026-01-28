using Application.Interfaces;
using Application.UseCases.UserPlugin.Common;
using MediatR;

namespace Application.UseCases.UserPlugin.ListUserPlugins;

public sealed class ListUserPluginsHandler(
    IUserPluginRepository userPluginRepository,
    IAgentRepository agentRepository
) : IRequestHandler<ListUserPluginsQuery, List<UserPluginDto>>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IAgentRepository _agentRepository = agentRepository;

    public async Task<List<UserPluginDto>> Handle(ListUserPluginsQuery request, CancellationToken cancellationToken)
    {
        // Get all plugins installed by the user
        var userPlugins = await _userPluginRepository.ListByUserAsync(request.UserId, cancellationToken);

        if (userPlugins.Count == 0)
        {
            return new List<UserPluginDto>();
        }

        // Get all agents owned by the user to compute EnabledAgents
        var userAgents = await _agentRepository.ListByUserAsync(request.UserId, cancellationToken);

        // Build a lookup: pluginId -> list of agentIds that have this plugin enabled
        var pluginToAgents = new Dictionary<Guid, List<Guid>>();
        foreach (var agent in userAgents)
        {
            if (agent.PluginIds == null || agent.PluginIds.Length == 0)
                continue;

            foreach (var pluginId in agent.PluginIds)
            {
                if (!pluginToAgents.ContainsKey(pluginId))
                {
                    pluginToAgents[pluginId] = new List<Guid>();
                }
                pluginToAgents[pluginId].Add(agent.Id);
            }
        }

        // Map to DTOs
        return userPlugins
            .Select(up =>
            {
                pluginToAgents.TryGetValue(up.PluginId, out var enabledAgents);
                return UserPluginDto.FromEntity(up, enabledAgents ?? Enumerable.Empty<Guid>());
            })
            .ToList();
    }
}
