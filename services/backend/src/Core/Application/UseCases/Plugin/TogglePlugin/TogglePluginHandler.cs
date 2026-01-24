using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.Plugin.TogglePlugin;

public class TogglePluginHandler(IPluginRepository repository) : IRequestHandler<TogglePluginCommand, Unit>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<Unit> Handle(TogglePluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (plugin is null)
            throw new PluginNotFoundException(request.Id);

        plugin.IsActive = request.Activate;
        plugin.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(plugin, cancellationToken);
        return Unit.Value;
    }
}