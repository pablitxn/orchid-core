using Application.Interfaces;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Plugin.CreatePlugin;

public class CreatePluginHandler(IPluginRepository repository) : IRequestHandler<CreatePluginCommand, PluginEntity>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<PluginEntity> Handle(CreatePluginCommand request, CancellationToken cancellationToken)
    {
        var plugin = new PluginEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            SourceUrl = request.SourceUrl,
            IsActive = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(plugin, cancellationToken);
        return plugin;
    }
}