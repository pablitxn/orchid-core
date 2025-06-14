using Domain.Entities;

namespace Application.Interfaces;

public interface ICreditTrackingService
{
    Task<CreditConsumptionEntity> TrackPluginUsageAsync(Guid userId, Guid pluginId, string pluginName, int credits, string? metadata = null, CancellationToken cancellationToken = default);
    Task<CreditConsumptionEntity> TrackWorkflowUsageAsync(Guid userId, Guid workflowId, string workflowName, int credits, string? metadata = null, CancellationToken cancellationToken = default);
    Task<CreditConsumptionEntity> TrackMessageCostAsync(Guid userId, Guid messageId, string messageDescription, int credits, CancellationToken cancellationToken = default);
    Task<bool> ValidateSufficientCreditsAsync(Guid userId, int requiredCredits, CancellationToken cancellationToken = default);
}