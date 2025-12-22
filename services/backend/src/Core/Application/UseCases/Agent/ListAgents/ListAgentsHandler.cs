using Application.Interfaces;
using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.ListAgents;

public class ListAgentsHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository,
    IChatSessionRepository chatSessionRepository) : IRequestHandler<ListAgentsQuery, List<AgentDto>>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IChatSessionRepository _chatSessionRepository = chatSessionRepository;

    public async Task<List<AgentDto>> Handle(ListAgentsQuery request, CancellationToken cancellationToken)
    {
        // MED-001: Filtering now done at database level for performance
        var filteredAgents = await _agentRepository.ListByUserAsync(request.UserId, cancellationToken);

        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginLookup = plugins.ToDictionary(p => p.Id);

        // MED-002: Fetch real stats for all agents in one query
        var agentIds = filteredAgents.Select(a => a.Id).ToList();
        var stats = await _chatSessionRepository.GetStatsForAgentsAsync(agentIds, cancellationToken);

        var agentDtos = filteredAgents.Select(agent =>
        {
            stats.TryGetValue(agent.Id, out var agentStats);

            return new AgentDto
            {
                Id = agent.Id,
                Name = agent.Name,
                Description = agent.Description,
                AvatarUrl = agent.AvatarUrl,
                Personality = agent.Personality,
                PersonalityTemplateId = agent.PersonalityTemplateId,
                Language = agent.Language,
                UserId = agent.UserId,
                IsPublic = agent.IsPublic,
                CreatedAt = agent.CreatedAt,
                UpdatedAt = agent.UpdatedAt,
                Plugins = agent.PluginIds
                    .Where(id => pluginLookup.ContainsKey(id))
                    .Select(id =>
                    {
                        var plugin = pluginLookup[id];
                        return new PluginDto
                        {
                            Id = plugin.Id,
                            Name = plugin.Name,
                            Description = plugin.Description,
                            Icon = null
                        };
                    })
                    .ToList(),
                Stats = new AgentStatsDto
                {
                    TotalConversations = agentStats.Count,
                    AvgRating = null, // Rating system not yet implemented
                    LastUsed = agentStats.LastUsed
                }
            };
        }).ToList();

        return agentDtos;
    }
}