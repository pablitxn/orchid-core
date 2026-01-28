using Application.Interfaces;
using Application.UseCases.UserPlugin.Common;
using MediatR;

namespace Application.UseCases.UserPlugin.GetUserPlugin;

public sealed class GetUserPluginHandler(
    IUserPluginRepository userPluginRepository,
    IAgentRepository agentRepository
) : IRequestHandler<GetUserPluginQuery, UserPluginDto?>
{
    private readonly IUserPluginRepository _userPluginRepository = userPluginRepository;
    private readonly IAgentRepository _agentRepository = agentRepository;

    public async Task<UserPluginDto?> Handle(GetUserPluginQuery request, CancellationToken cancellationToken)
    {
        var userPlugin = await _userPluginRepository.GetByUserAndPluginAsync(
            request.UserId, request.PluginId, cancellationToken);

        if (userPlugin == null)
        {
            return null;
        }

        // Get agents that have this plugin enabled
        var userAgents = await _agentRepository.ListByUserAsync(request.UserId, cancellationToken);
        var enabledAgentIds = userAgents
            .Where(a => a.PluginIds != null && a.PluginIds.Contains(request.PluginId))
            .Select(a => a.Id)
            .ToList();

        return UserPluginDto.FromEntity(userPlugin, enabledAgentIds);
    }
}
