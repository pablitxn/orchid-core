using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Plugin.DeletePlugin;

public sealed class DeletePluginHandler(
    IPluginRepository pluginRepository,
    IUserPluginRepository userPluginRepository,
    IAgentRepository agentRepository,
    ILogger<DeletePluginHandler> logger)
    : IRequestHandler<DeletePluginCommand, Unit>
{
    public async Task<Unit> Handle(DeletePluginCommand command, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting plugin {PluginId} for user {UserId}", command.PluginId, command.UserId);

        // Check if plugin exists
        var plugin = await pluginRepository.GetByIdAsync(command.PluginId, cancellationToken);
        if (plugin == null)
        {
            throw new InvalidOperationException($"Plugin with ID {command.PluginId} not found");
        }

        // Check if user owns the plugin
        var userOwnsPlugin = await userPluginRepository.UserOwnsPluginAsync(command.UserId, command.PluginId, cancellationToken);
        if (!userOwnsPlugin)
        {
            throw new UnauthorizedAccessException($"User {command.UserId} does not own plugin {command.PluginId}");
        }

        // Find all agents that use this plugin and remove it from them
        var allAgents = await agentRepository.ListAsync(cancellationToken);
        var agentsWithPlugin = allAgents.Where(a => a.PluginIds.Contains(command.PluginId)).ToList();
        
        foreach (var agent in agentsWithPlugin)
        {
            agent.PluginIds = agent.PluginIds.Where(id => id != command.PluginId).ToArray();
            agent.UpdatedAt = DateTime.UtcNow;
            await agentRepository.UpdateAsync(agent, cancellationToken);
            logger.LogInformation("Removed plugin {PluginId} from agent {AgentId}", command.PluginId, agent.Id);
        }

        // Remove user-plugin association
        await userPluginRepository.DeleteByUserAndPluginAsync(command.UserId, command.PluginId, cancellationToken);
        logger.LogInformation("Removed user-plugin association for user {UserId} and plugin {PluginId}", command.UserId, command.PluginId);

        return Unit.Value;
    }
}