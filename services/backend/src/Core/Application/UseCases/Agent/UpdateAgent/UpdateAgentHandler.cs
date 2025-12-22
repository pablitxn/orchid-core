using Application.Interfaces;
using Application.UseCases.Agent.Common;
using MediatR;

namespace Application.UseCases.Agent.UpdateAgent;

public sealed class UpdateAgentHandler(
    IAgentRepository agentRepository,
    IPluginRepository pluginRepository,
    IChatSessionRepository chatSessionRepository) : IRequestHandler<UpdateAgentCommand, AgentDto>
{
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginRepository _pluginRepository = pluginRepository;
    private readonly IChatSessionRepository _chatSessionRepository = chatSessionRepository;

    public async Task<AgentDto> Handle(UpdateAgentCommand request, CancellationToken cancellationToken)
    {
        UpdateAgentCommandValidator.Validate(request);

        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);

        if (agent is null)
        {
            throw new KeyNotFoundException($"Agent with ID {request.AgentId} not found");
        }

        if (agent.UserId != request.UserId)
        {
            throw new UnauthorizedAccessException("You don't have permission to update this agent");
        }

        if (agent.IsDeleted)
        {
            throw new InvalidOperationException("Cannot update a deleted agent");
        }

        // Update only provided fields
        if (request.Name is not null)
            agent.Name = request.Name;

        if (request.Description is not null)
            agent.Description = request.Description;

        if (request.AvatarUrl is not null)
            agent.AvatarUrl = request.AvatarUrl;

        if (request.Personality is not null)
            agent.Personality = request.Personality;

        if (request.PersonalityTemplateId.HasValue)
            agent.PersonalityTemplateId = request.PersonalityTemplateId;

        if (request.Language is not null)
            agent.Language = request.Language;

        if (request.PluginIds is not null)
            agent.PluginIds = request.PluginIds;

        if (request.IsPublic.HasValue)
            agent.IsPublic = request.IsPublic.Value;

        agent.UpdatedAt = DateTime.UtcNow;

        await _agentRepository.UpdateAsync(agent, cancellationToken);

        // Fetch plugin details for the response
        var plugins = await _pluginRepository.ListAsync(cancellationToken);
        var pluginLookup = plugins.ToDictionary(p => p.Id);

        // Fetch real stats for the agent
        var conversationCount = await _chatSessionRepository.CountByAgentIdAsync(agent.Id, cancellationToken);
        var lastUsed = await _chatSessionRepository.GetLastUsedByAgentIdAsync(agent.Id, cancellationToken);

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
                TotalConversations = conversationCount,
                AvgRating = null, // Rating system not yet implemented
                LastUsed = lastUsed
            }
        };
    }
}
