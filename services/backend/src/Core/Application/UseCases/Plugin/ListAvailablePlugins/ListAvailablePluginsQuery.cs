using MediatR;

namespace Application.UseCases.Plugin.ListAvailablePluginDtos;

public sealed record ListAvailablePluginDtosQuery(
    Guid UserId
) : IRequest<ListAvailablePluginDtosDto>;

public sealed record ListAvailablePluginDtosDto(
    IEnumerable<AvailablePluginDto> Plugins
);

public sealed record AvailablePluginDto(
    Guid Id,
    string Name,
    string? Description,
    int PriceCredits,
    bool IsSubscriptionBased,
    bool UserOwns,
    bool IsActive
);