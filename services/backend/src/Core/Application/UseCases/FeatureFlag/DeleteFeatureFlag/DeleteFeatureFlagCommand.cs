using MediatR;

namespace Application.UseCases.FeatureFlag.DeleteFeatureFlag;

public record DeleteFeatureFlagCommand(Guid Id) : IRequest<Unit>;
