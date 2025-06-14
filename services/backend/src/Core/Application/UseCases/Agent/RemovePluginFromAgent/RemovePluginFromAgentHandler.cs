using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Agent.RemovePluginFromAgent;

public sealed class RemovePluginFromAgentHandler(
    IAgentRepository agentRepository,
    ILogger<RemovePluginFromAgentHandler> logger
) : IRequestHandler<RemovePluginFromAgentCommand, RemovePluginFromAgentResult>
{
    public async Task<RemovePluginFromAgentResult> Handle(RemovePluginFromAgentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Get the agent
            var agent = await agentRepository.GetByIdAsync(command.AgentId, cancellationToken);
            if (agent == null)
            {
                return new RemovePluginFromAgentResult(false, "Agent not found");
            }

            // Check if plugin is in the agent
            if (!agent.PluginIds.Contains(command.PluginId))
            {
                return new RemovePluginFromAgentResult(false, "Plugin is not assigned to this agent");
            }

            // Remove plugin from agent
            agent.PluginIds = agent.PluginIds.Where(id => id != command.PluginId).ToArray();
            agent.UpdatedAt = DateTime.UtcNow;

            await agentRepository.UpdateAsync(agent, cancellationToken);

            logger.LogInformation("Plugin {PluginId} removed from agent {AgentId} by user {UserId}", 
                command.PluginId, command.AgentId, command.UserId);

            return new RemovePluginFromAgentResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing plugin {PluginId} from agent {AgentId}", 
                command.PluginId, command.AgentId);
            return new RemovePluginFromAgentResult(false, "An error occurred while removing the plugin");
        }
    }
}