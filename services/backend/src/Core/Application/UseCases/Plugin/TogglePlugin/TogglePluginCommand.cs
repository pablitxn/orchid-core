using MediatR;

namespace Application.UseCases.Plugin.TogglePlugin;

public class TogglePluginCommand(Guid id, bool activate) : IRequest<bool>
{
    public Guid Id { get; } = id;
    public bool Activate { get; } = activate;
}