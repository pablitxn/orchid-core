using Application.Interfaces;
using MediatR;

namespace Application.UseCases.Plugin.TogglePlugin;

public class TogglePluginHandler(IPluginRepository repository) : IRequestHandler<TogglePluginCommand, bool>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<bool> Handle(TogglePluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (plugin is null) return false;

        plugin.IsActive = request.Activate;
        plugin.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(plugin, cancellationToken);
        return true;
    }
}