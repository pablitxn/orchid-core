using Domain.Entities;

namespace Application.Interfaces;

public interface IPluginDiscoveryService
{
    Task<IEnumerable<DiscoveredPlugin>> DiscoverPluginsAsync(CancellationToken cancellationToken = default);
}

public record DiscoveredPlugin(
    string Name,
    string SystemName,
    string Description,
    IEnumerable<string> Functions,
    int DefaultPriceCredits = 10,
    bool IsSubscriptionBased = false
);