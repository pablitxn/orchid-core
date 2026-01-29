using Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.UserPlugin.UninstallPlugin;

public sealed class UninstallPluginHandler(
    IUnitOfWork unitOfWork,
    IAgentRepository agentRepository,
    IPluginUsageTracker pluginUsageTracker,
    ILogger<UninstallPluginHandler> logger
) : IRequestHandler<UninstallPluginCommand, UninstallPluginDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAgentRepository _agentRepository = agentRepository;
    private readonly IPluginUsageTracker _pluginUsageTracker = pluginUsageTracker;
    private readonly ILogger<UninstallPluginHandler> _logger = logger;

    public async Task<UninstallPluginDto> Handle(UninstallPluginCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _unitOfWork.BeginTransactionAsync(cancellationToken);

            // Check if user owns the plugin
            var userPlugin = await _unitOfWork.UserPlugins.GetByUserAndPluginAsync(
                command.UserId, command.PluginId, cancellationToken);

            if (userPlugin == null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new UninstallPluginDto(false, "Plugin not found or not installed");
            }

            if (!userPlugin.IsActive)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return new UninstallPluginDto(false, "Plugin is already uninstalled");
            }

            // Soft-delete the user plugin
            userPlugin.IsActive = false;
            await _unitOfWork.UserPlugins.UpdateAsync(userPlugin, cancellationToken);

            // Remove plugin from all user's agents
            var userAgents = await _agentRepository.ListByUserAsync(command.UserId, cancellationToken);
            foreach (var agent in userAgents)
            {
                if (agent.PluginIds != null && agent.PluginIds.Contains(command.PluginId))
                {
                    agent.PluginIds = agent.PluginIds.Where(id => id != command.PluginId).ToArray();
                    agent.UpdatedAt = DateTime.UtcNow;
                    await _agentRepository.UpdateAsync(agent, cancellationToken);
                }
            }

            // Decrement InstallCount on the plugin
            var plugin = await _unitOfWork.Plugins.GetByIdAsync(command.PluginId, cancellationToken);
            if (plugin != null && plugin.InstallCount > 0)
            {
                plugin.InstallCount--;
                plugin.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.Plugins.UpdateAsync(plugin, cancellationToken);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("User {UserId} uninstalled plugin {PluginId}", command.UserId, command.PluginId);

            // Track plugin uninstallation for analytics (non-blocking)
            if (plugin != null)
            {
                _ = _pluginUsageTracker.TrackUninstallAsync(plugin.Id, command.UserId, plugin.Name, cancellationToken);
            }

            return new UninstallPluginDto(true);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.LogError(ex, "Error uninstalling plugin {PluginId} for user {UserId}",
                command.PluginId, command.UserId);
            return new UninstallPluginDto(false, "An error occurred while uninstalling the plugin");
        }
    }
}
