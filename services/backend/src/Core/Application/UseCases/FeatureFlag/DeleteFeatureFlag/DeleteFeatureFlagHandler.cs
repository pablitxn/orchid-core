using Application.Interfaces;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.FeatureFlag.DeleteFeatureFlag;

public class DeleteFeatureFlagHandler(
    IFeatureFlagRepository repository,
    ILogger<DeleteFeatureFlagHandler> logger) : IRequestHandler<DeleteFeatureFlagCommand, Unit>
{
    public async Task<Unit> Handle(DeleteFeatureFlagCommand request, CancellationToken cancellationToken)
    {
        var featureFlag = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new FeatureFlagNotFoundException(request.Id);

        await repository.DeleteAsync(request.Id, cancellationToken);

        logger.LogInformation(
            "Feature flag deleted: {FeatureFlagKey} (Id: {FeatureFlagId})",
            featureFlag.Key,
            featureFlag.Id);

        return Unit.Value;
    }
}
