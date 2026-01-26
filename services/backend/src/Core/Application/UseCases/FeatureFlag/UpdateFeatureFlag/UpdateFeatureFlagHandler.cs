using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.FeatureFlag.UpdateFeatureFlag;

public class UpdateFeatureFlagHandler(
    IFeatureFlagRepository repository,
    ILogger<UpdateFeatureFlagHandler> logger) : IRequestHandler<UpdateFeatureFlagCommand, FeatureFlagDto>
{
    public async Task<FeatureFlagDto> Handle(UpdateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var featureFlag = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FeatureFlagNotFoundException(request.Id);

        // Validate rollout percentage
        if (request.RolloutPercentage is < 0 or > 100)
        {
            throw new ValidationException("Rollout percentage must be between 0 and 100.");
        }

        var previousEnabled = featureFlag.IsEnabled;

        featureFlag.Description = request.Description;
        featureFlag.IsEnabled = request.IsEnabled;
        featureFlag.AllowedPlans = request.AllowedPlans;
        featureFlag.RolloutPercentage = request.RolloutPercentage;

        await repository.UpdateAsync(featureFlag, cancellationToken);

        logger.LogInformation(
            "Feature flag updated: {FeatureFlagKey} (Id: {FeatureFlagId}, Enabled: {PreviousEnabled} -> {IsEnabled})",
            featureFlag.Key,
            featureFlag.Id,
            previousEnabled,
            featureFlag.IsEnabled);

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
