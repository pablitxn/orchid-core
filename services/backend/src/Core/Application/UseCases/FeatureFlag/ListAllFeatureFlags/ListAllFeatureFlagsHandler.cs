using Application.Interfaces;
using MediatR;

namespace Application.UseCases.FeatureFlag.ListAllFeatureFlags;

public class ListAllFeatureFlagsHandler(IFeatureFlagRepository repository)
    : IRequestHandler<ListAllFeatureFlagsQuery, ListAllFeatureFlagsDto>
{
    public async Task<ListAllFeatureFlagsDto> Handle(ListAllFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var featureFlags = await repository.ListAsync(cancellationToken);

        var dtos = featureFlags.Select(f => new FeatureFlagDto(
            f.Id,
            f.Key,
            f.Description,
            f.IsEnabled,
            f.AllowedPlans,
            f.RolloutPercentage,
            f.CreatedAt,
            f.UpdatedAt)).ToList();

        return new ListAllFeatureFlagsDto(dtos);
    }
}
