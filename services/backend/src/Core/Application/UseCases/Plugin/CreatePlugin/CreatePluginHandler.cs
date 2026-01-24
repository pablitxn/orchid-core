using Application.Interfaces;
using Application.UseCases.Plugin.Common;
using Domain.Entities;
using MediatR;

namespace Application.UseCases.Plugin.CreatePlugin;

public class CreatePluginHandler(IPluginRepository repository) : IRequestHandler<CreatePluginCommand, PluginDto>
{
    private readonly IPluginRepository _repository = repository;

    public async Task<PluginDto> Handle(CreatePluginCommand request, CancellationToken cancellationToken)
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
        return PluginDto.FromEntity(plugin);
    }
}
