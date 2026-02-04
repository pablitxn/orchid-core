using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

public class CreditTrackingService : ICreditTrackingService
{
    private readonly ICreditConsumptionRepository _creditConsumptionRepository;
    private readonly IUserCreditsRepository _userCreditsRepository;
    private readonly ILogger<CreditTrackingService> _logger;

    public CreditTrackingService(
        ICreditConsumptionRepository creditConsumptionRepository,
        IUserCreditsRepository userCreditsRepository,
        ILogger<CreditTrackingService> logger)
    {
        _creditConsumptionRepository = creditConsumptionRepository;
        _userCreditsRepository = userCreditsRepository;
        _logger = logger;
    }

    public async Task<CreditConsumptionEntity> TrackPluginUsageAsync(
        Guid userId,
        Guid pluginId,
        string pluginName,
        int credits,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var userCredits = await _userCreditsRepository.GetByUserIdAsync(userId, cancellationToken);
        var balanceAfter = userCredits?.AvailableCredits ?? 0;

        var consumption = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConsumptionType = "plugin",
            ResourceId = pluginId,
            ResourceName = pluginName,
            CreditsConsumed = credits,
            Metadata = metadata,
            ConsumedAt = DateTime.UtcNow,
            BalanceAfter = balanceAfter
        };

        await _creditConsumptionRepository.CreateAsync(consumption, cancellationToken);

        ApplicationMetrics.CreditsConsumedTotal.WithLabels("plugin", pluginName).Inc(credits);

        _logger.LogInformation("Tracked plugin usage: User {UserId}, Plugin {PluginName}, Credits {Credits}",
            userId, pluginName, credits);

        return consumption;
    }

    public async Task<CreditConsumptionEntity> TrackWorkflowUsageAsync(
        Guid userId,
        Guid workflowId,
        string workflowName,
        int credits,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var userCredits = await _userCreditsRepository.GetByUserIdAsync(userId, cancellationToken);
        var balanceAfter = userCredits?.AvailableCredits ?? 0;

        var consumption = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConsumptionType = "workflow",
            ResourceId = workflowId,
            ResourceName = workflowName,
            CreditsConsumed = credits,
            Metadata = metadata,
            ConsumedAt = DateTime.UtcNow,
            BalanceAfter = balanceAfter
        };

        await _creditConsumptionRepository.CreateAsync(consumption, cancellationToken);

        ApplicationMetrics.CreditsConsumedTotal.WithLabels("workflow", workflowName).Inc(credits);

        _logger.LogInformation("Tracked workflow usage: User {UserId}, Workflow {WorkflowName}, Credits {Credits}",
            userId, workflowName, credits);

        return consumption;
    }

    public async Task<CreditConsumptionEntity> TrackMessageCostAsync(
        Guid userId,
        Guid messageId,
        string messageDescription,
        int credits,
        CancellationToken cancellationToken = default)
    {
        var userCredits = await _userCreditsRepository.GetByUserIdAsync(userId, cancellationToken);
        var balanceAfter = userCredits?.AvailableCredits ?? 0;

        var consumption = new CreditConsumptionEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ConsumptionType = "message",
            ResourceId = messageId,
            ResourceName = messageDescription,
            CreditsConsumed = credits,
            ConsumedAt = DateTime.UtcNow,
            BalanceAfter = balanceAfter
        };

        await _creditConsumptionRepository.CreateAsync(consumption, cancellationToken);

        ApplicationMetrics.CreditsConsumedTotal.WithLabels("message", "chat").Inc(credits);

        _logger.LogInformation("Tracked message cost: User {UserId}, Message {MessageId}, Credits {Credits}",
            userId, messageId, credits);

        return consumption;
    }

    public async Task<bool> ValidateSufficientCreditsAsync(
        Guid userId,
        int requiredCredits,
        CancellationToken cancellationToken = default)
    {
        var userCredits = await _userCreditsRepository.GetByUserIdAsync(userId, cancellationToken);
        if (userCredits == null)
        {
            _logger.LogWarning("User {UserId} has no credits record", userId);
            return false;
        }

        var hasCredits = userCredits.AvailableCredits >= requiredCredits;

        if (!hasCredits)
        {
            _logger.LogWarning("User {UserId} has insufficient credits: {Available} < {Required}",
                userId, userCredits.AvailableCredits, requiredCredits);
        }

        return hasCredits;
    }
}
