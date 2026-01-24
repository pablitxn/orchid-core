using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePlugins;

public sealed record ListAvailablePluginsQuery(
    Guid UserId
) : IRequest<ListAvailablePluginsDto>;

public sealed record ListAvailablePluginsDto(
    IEnumerable<AvailablePlugin> Plugins
);

public sealed record AvailablePlugin(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool UserOwns,
    bool IsActive
);