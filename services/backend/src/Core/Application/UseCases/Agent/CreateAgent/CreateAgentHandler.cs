using Application.Interfaces;
using Application.UseCases.Agent.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Agent.CreateAgent;

public class CreateAgentHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository) : IRequestHandler<CreateAgentCommand, AgentDto>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;

    public async Task<AgentDto> Handle(CreateAgentCommand request, CancellationToken cancellationToken)
    {
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            AvatarUrl = request.AvatarUrl,
            Personality = request.Personality,
            PersonalityTemplateId = request.PersonalityTemplateId,
            Language = request.Language,
            PluginIds = request.PluginIds,
            UserId = request.UserId,
            IsPublic = request.IsPublic,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _agentRepository.CreateAsync(agent, cancellationToken);
        
        // Fetch plugin details for the response
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginLookup = plugins.ToDictionary(p => p.Id);
        
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
                TotalConversations = 0,
                AvgRating = null,
                LastUsed = null
            }
        };
    }
}