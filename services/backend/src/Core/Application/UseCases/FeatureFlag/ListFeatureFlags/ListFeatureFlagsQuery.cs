using MediatR;

namespace Application.UseCases.FeatureFlag.ListFeatureFlags;

public record ListFeatureFlagsQuery(Guid? UserId = null, string? UserPlan = null) : IRequest<ListFeatureFlagsDto>;

public record ListFeatureFlagsDto(Dictionary<string, bool> Features);
