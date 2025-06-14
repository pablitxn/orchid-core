using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Agent.AddPluginToAgent;

public sealed class AddPluginToAgentHandler(
    IAgentRepository agentRepository,
    IUserPluginRepository userPluginRepository,
    ILogger<AddPluginToAgentHandler> logger
) : IRequestHandler<AddPluginToAgentCommand, AddPluginToAgentResult>
{
    public async Task<AddPluginToAgentResult> Handle(AddPluginToAgentCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // Verify user owns the plugin
            var userOwnsPlugin = await userPluginRepository.UserOwnsPluginAsync(
                command.UserId, command.PluginId, cancellationToken);
            
            if (!userOwnsPlugin)
            {
                return new AddPluginToAgentResult(false, 
                    "You must purchase this plugin before adding it to an agent");
            }

            // Get the agent
            var agent = await agentRepository.GetByIdAsync(command.AgentId, cancellationToken);
            if (agent == null)
            {
                return new AddPluginToAgentResult(false, "Agent not found");
            }

            // Check if plugin is already added
            if (agent.PluginIds.Contains(command.PluginId))
            {
                return new AddPluginToAgentResult(false, "Plugin is already added to this agent");
            }

            // Add plugin to agent
            var updatedPluginIds = agent.PluginIds.ToList();
            updatedPluginIds.Add(command.PluginId);
            agent.PluginIds = updatedPluginIds.ToArray();
            agent.UpdatedAt = DateTime.UtcNow;

            await agentRepository.UpdateAsync(agent, cancellationToken);

            logger.LogInformation("Plugin {PluginId} added to agent {AgentId} by user {UserId}", 
                command.PluginId, command.AgentId, command.UserId);

            return new AddPluginToAgentResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding plugin {PluginId} to agent {AgentId}", 
                command.PluginId, command.AgentId);
            return new AddPluginToAgentResult(false, "An error occurred while adding the plugin");
        }
    }
}