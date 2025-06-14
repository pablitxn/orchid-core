using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Agent.VerifyAgentAccess;

public class VerifyAgentAccessHandler : IRequestHandler<VerifyAgentAccessQuery, VerifyAgentAccessResult>
{
    private readonly IAgentRepository _agentRepository;
    private readonly IUserPluginRepository _userPluginRepository;

    public VerifyAgentAccessHandler(
        IAgentRepository agentRepository,
        IUserPluginRepository userPluginRepository)
    {
        _agentRepository = agentRepository;
        _userPluginRepository = userPluginRepository;
    }

    public async Task<VerifyAgentAccessResult> Handle(
        VerifyAgentAccessQuery request, 
        CancellationToken cancellationToken)
    {
        // Get the agent
        var agent = await _agentRepository.GetByIdAsync(request.AgentId, cancellationToken);
        
        if (agent == null)
        {
            return new VerifyAgentAccessResult(false, "Agent not found");
        }

        // If the agent is private, only the owner can access it
        if (!agent.IsPublic && agent.UserId != request.UserId)
        {
            return new VerifyAgentAccessResult(false, "Agent is private and you are not the owner");
        }

        // If the agent is public or owned by the user, check plugin availability
        if (agent.PluginIds?.Length > 0)
        {
            var missingPlugins = new List<Guid>();
            
            foreach (var pluginId in agent.PluginIds)
            {
                var userOwnsPlugin = await _userPluginRepository.UserOwnsPluginAsync(
                    request.UserId, 
                    pluginId, 
                    cancellationToken);
                
                if (!userOwnsPlugin)
                {
                    missingPlugins.Add(pluginId);
                }
            }

            if (missingPlugins.Count > 0)
            {
                return new VerifyAgentAccessResult(
                    false, 
                    "You don't have access to all required plugins", 
                    missingPlugins);
            }
        }

        // User has access
        return new VerifyAgentAccessResult(true);
    }
}