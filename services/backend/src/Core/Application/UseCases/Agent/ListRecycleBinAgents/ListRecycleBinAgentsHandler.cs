using Application.Interfaces;
using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.ListRecycleBinAgents;

public sealed class ListRecycleBinAgentsHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository) : IRequestHandler<ListRecycleBinAgentsQuery, List<AgentDto>>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<List<AgentDto>> Handle(ListRecycleBinAgentsQuery request, CancellationToken cancellationToken)
    {
        var agents = await _agentRepository.ListRecycleBinByUserAsync(request.UserId, cancellationToken);

        // Fetch plugin details for the response
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginLookup = plugins.ToDictionary(p => p.Id);

        return agents.Select(agent => new AgentDto
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
                TotalConversations = 0,
                AvgRating = null,
                LastUsed = null
            }
        }).ToList();
    }
}