namespace Application.UseCases.FeatureFlag;

public record FeatureFlagDto(
    Guid Id,
    string Key,
    string? Description,
    bool IsEnabled,
    string? AllowedPlans,
    int RolloutPercentage,
    DateTime CreatedAt,
    DateTime UpdatedAt);
