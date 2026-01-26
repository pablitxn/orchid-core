using Application.Interfaces;
using Domain.Exceptions;
using MediatR;

namespace Application.UseCases.FeatureFlag.GetFeatureFlagById;

public class GetFeatureFlagByIdHandler(IFeatureFlagRepository repository)
    : IRequestHandler<GetFeatureFlagByIdQuery, FeatureFlagDto>
{
    public async Task<FeatureFlagDto> Handle(GetFeatureFlagByIdQuery request, CancellationToken cancellationToken)
    {
        var featureFlag = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FeatureFlagNotFoundException(request.Id);

        return new FeatureFlagDto(
            featureFlag.Id,
            featureFlag.Key,
            featureFlag.Description,
            featureFlag.IsEnabled,
            featureFlag.AllowedPlans,
            featureFlag.RolloutPercentage,
            featureFlag.CreatedAt,
            featureFlag.UpdatedAt);
    }
}
