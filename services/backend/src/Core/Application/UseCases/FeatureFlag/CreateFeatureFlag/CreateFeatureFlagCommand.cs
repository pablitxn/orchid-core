using MediatR;

namespace Application.UseCases.FeatureFlag.CreateFeatureFlag;

public record CreateFeatureFlagCommand(
    string Key,
    string? Description,
    bool IsEnabled,
    string? AllowedPlans,
    int RolloutPercentage) : IRequest<FeatureFlagDto>;
