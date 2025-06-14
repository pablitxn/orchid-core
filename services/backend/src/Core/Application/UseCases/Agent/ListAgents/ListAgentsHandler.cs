using Application.Interfaces;
using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.ListAgents;

public class ListAgentsHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository) : IRequestHandler<ListAgentsQuery, List<AgentDto>>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<List<AgentDto>> Handle(ListAgentsQuery request, CancellationToken cancellationToken)
    {
        var agents = await _agentRepository.ListAsync(cancellationToken);
        
        // Filter agents: show public agents and user's private agents
        var filteredAgents = agents.Where(agent => 
            agent.IsPublic || 
            (request.UserId.HasValue && agent.UserId == request.UserId.Value)
        ).ToList();
        
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        
        var pluginLookup = plugins.ToDictionary(p => p.Id);
        
        var agentDtos = filteredAgents.Select(agent => new AgentDto
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
                        Icon = null // Add icon field to PluginEntity if needed
                    };
                })
                .ToList(),
            Stats = new AgentStatsDto
            {
                TotalConversations = 0, // TODO: Implement stats tracking
                AvgRating = null,
                LastUsed = null
            }
        }).ToList();

        return agentDtos;
    }
}