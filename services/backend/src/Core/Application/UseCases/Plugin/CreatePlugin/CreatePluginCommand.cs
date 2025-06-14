using Domain.Entities;
using MediatR;

namespace Application.UseCases.Plugin.CreatePlugin;

public class CreatePluginCommand(string name, string? description, string? sourceUrl) : IRequest<PluginEntity>
{
    public string Name { get; } = name;
    public string? Description { get; } = description;
    public string? SourceUrl { get; } = sourceUrl;
}