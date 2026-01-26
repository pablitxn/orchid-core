using MediatR;

namespace Application.UseCases.FeatureFlag.ListAllFeatureFlags;

public record ListAllFeatureFlagsQuery : IRequest<ListAllFeatureFlagsDto>;

public record ListAllFeatureFlagsDto(List<FeatureFlagDto> FeatureFlags);
