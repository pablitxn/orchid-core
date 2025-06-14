using Application.Interfaces;
using MediatR;

namespace Application.UseCases.ChatSession.GetChatSessionById;

public class GetChatSessionByIdHandler(IChatSessionRepository repo, IPluginRepository pluginRepo)
    : IRequestHandler<GetChatSessionByIdQuery, GetChatSessionByIdDto?>
{
    private readonly IChatSessionRepository _repo = repo;
    private readonly IPluginRepository _pluginRepo = pluginRepo;

    public async Task<GetChatSessionByIdDto?> Handle(GetChatSessionByIdQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _repo.GetBySessionIdAsync(request.SessionId, cancellationToken);
        
        if (session is null)
            return null;

        AgentDto? agentDto = null;
        if (session.Agent is not null)
        {
            // Get plugins for this agent
            var agentPlugins = await _pluginRepo.ListByAgentAsync(session.Agent.Id, cancellationToken);
            var plugins = agentPlugins
                .Select(p => new PluginDto(
                    p.Id,
                    p.Name,
                    p.Description ?? string.Empty,
                    "1.0.0", // Default version since it's not in PluginEntity
                    p.PriceCredits, // Using PriceCredits as the price
                    "General", // Default category since it's not in PluginEntity
                    p.IsActive
                ))
                .ToList();

            agentDto = new AgentDto(
                session.Agent.Id,
                session.Agent.Name,
                session.Agent.Description,
                null, // Prompt is not available in AgentEntity
                "gpt-4", // Default model since it's not in AgentEntity
                true, // Default IsActive since it's not in AgentEntity
                session.Agent.IsPublic,
                session.Agent.UserId ?? Guid.Empty,
                plugins
            );
        }

        TeamDto? teamDto = null;
        if (session.Team is not null)
        {
            teamDto = new TeamDto(
                session.Team.Id,
                session.Team.Name,
                session.Team.Description
            );
        }

        return new GetChatSessionByIdDto(
            session.Id,
            session.SessionId,
            session.UserId,
            session.AgentId,
            session.TeamId,
            session.Title ?? string.Empty,
            session.InteractionType.ToString(),
            session.CreatedAt,
            session.UpdatedAt,
            agentDto,
            teamDto
        );
    }
}