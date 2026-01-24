using MediatR;

namespace Application.UseCases.Plugin.PurchasePlugin;

public sealed record PurchasePluginCommand(
    Guid UserId,
    Guid PluginId
) : IRequest<PurchasePluginDto>;

public sealed record PurchasePluginDto(
    bool Success,
    string? ErrorMessage = null
);