using MediatR;

namespace Application.UseCases.FeatureFlag.UpdateFeatureFlag;

public record UpdateFeatureFlagCommand(
    Guid Id,
    string? Description,
    bool IsEnabled,
    string? AllowedPlans,
    int RolloutPercentage) : IRequest<FeatureFlagDto>;
