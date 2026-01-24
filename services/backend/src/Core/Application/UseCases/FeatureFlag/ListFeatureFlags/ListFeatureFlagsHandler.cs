using Application.Interfaces;
using MediatR;

namespace Application.UseCases.FeatureFlag.ListFeatureFlags;

public class ListFeatureFlagsHandler(IFeatureFlagRepository repository) : IRequestHandler<ListFeatureFlagsQuery, ListFeatureFlagsDto>
{
    private readonly IFeatureFlagRepository _repository = repository;

    public async Task<ListFeatureFlagsDto> Handle(ListFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var featureFlags = await _repository.ListEnabledAsync(cancellationToken);

        var features = new Dictionary<string, bool>();

        foreach (var flag in featureFlags)
        {
            var isEnabled = EvaluateFeatureFlag(flag, request.UserId, request.UserPlan);
            features[flag.Key] = isEnabled;
        }

        return new ListFeatureFlagsDto(features);
    }

    private static bool EvaluateFeatureFlag(Domain.Entities.FeatureFlagEntity flag, Guid? userId, string? userPlan)
    {
        // If flag is disabled, return false
        if (!flag.IsEnabled)
            return false;

        // Check plan-based access
        if (!string.IsNullOrEmpty(flag.AllowedPlans))
        {
            var allowedPlans = flag.AllowedPlans.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // If user has no plan, check if "free" is allowed
            if (string.IsNullOrEmpty(userPlan))
            {
                if (!allowedPlans.Contains("free", StringComparer.OrdinalIgnoreCase))
                    return false;
            }
            else if (!allowedPlans.Contains(userPlan, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check rollout percentage (deterministic based on user ID)
        if (flag.RolloutPercentage < 100 && userId.HasValue)
        {
            var hash = Math.Abs(userId.Value.GetHashCode()) % 100;
            if (hash >= flag.RolloutPercentage)
                return false;
        }

        return true;
    }
}
