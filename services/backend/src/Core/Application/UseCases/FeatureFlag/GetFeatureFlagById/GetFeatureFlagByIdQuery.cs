using MediatR;

namespace Application.UseCases.FeatureFlag.GetFeatureFlagById;

public record GetFeatureFlagByIdQuery(Guid Id) : IRequest<FeatureFlagDto>;
