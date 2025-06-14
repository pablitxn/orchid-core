using MediatR;

namespace Application.UseCases.Plugin.PurchasePlugin;

public sealed record PurchasePluginCommand(
    Guid UserId,
    Guid PluginId
) : IRequest<PurchasePluginResult>;

public sealed record PurchasePluginResult(
    bool Success,
    string? ErrorMessage = null
);