using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.FeatureFlag.CreateFeatureFlag;

public class CreateFeatureFlagHandler(
    IFeatureFlagRepository repository,
    ILogger<CreateFeatureFlagHandler> logger) : IRequestHandler<CreateFeatureFlagCommand, FeatureFlagDto>
{
    public async Task<FeatureFlagDto> Handle(CreateFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        // Validate key uniqueness
        var existing = await repository.GetByKeyAsync(request.Key, cancellationToken);
        if (existing is not null)
        {
            throw new ValidationException($"A feature flag with key '{request.Key}' already exists.");
        }

        // Validate rollout percentage
        if (request.RolloutPercentage is < 0 or > 100)
        {
            throw new ValidationException("Rollout percentage must be between 0 and 100.");
        }

        var featureFlag = new FeatureFlagEntity
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Description = request.Description,
            IsEnabled = request.IsEnabled,
            AllowedPlans = request.AllowedPlans,
            RolloutPercentage = request.RolloutPercentage,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await repository.CreateAsync(featureFlag, cancellationToken);

        logger.LogInformation(
            "Feature flag created: {FeatureFlagKey} (Id: {FeatureFlagId}, Enabled: {IsEnabled})",
            featureFlag.Key,
            featureFlag.Id,
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
