using MediatR;

namespace Application.UseCases.FeatureFlag.ListFeatureFlags;

public record ListFeatureFlagsQuery(Guid? UserId = null, string? UserPlan = null) : IRequest<ListFeatureFlagsResponse>;

public record ListFeatureFlagsResponse(Dictionary<string, bool> Features);
