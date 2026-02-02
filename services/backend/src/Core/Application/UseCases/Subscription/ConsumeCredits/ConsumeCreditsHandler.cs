using Application.Interfaces;
using Application.UseCases.Subscription.Common;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.Subscription.ConsumeCredits;

public class ConsumeCreditsHandler(
    IUnitOfWork unitOfWork,
    IEventPublisher eventPublisher,
    ICreditLimitService creditLimitService,
    IAuditService auditService,
    ILogger<ConsumeCreditsHandler> logger)
    : IRequestHandler<ConsumeCreditsCommand, SubscriptionDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ICreditLimitService _creditLimitService = creditLimitService;
    private readonly IAuditService _auditService = auditService;
    private readonly ILogger<ConsumeCreditsHandler> _logger = logger;

    public async Task<SubscriptionDto> Handle(ConsumeCreditsCommand request, CancellationToken cancellationToken)
    {
        const int MAX_RETRY_ATTEMPTS = 3;
        int retryCount = 0;

        while (retryCount < MAX_RETRY_ATTEMPTS)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);

                // Check if user has an unlimited subscription (optional)
                var subscription = await _unitOfWork.Subscriptions.GetByUserIdAsync(request.UserId, cancellationToken);

                // If user has unlimited subscription, no credits are consumed
                if (subscription?.HasUnlimitedCredits() == true)
                {
                    await _unitOfWork.CommitTransactionAsync(cancellationToken);

                    _logger.LogInformation("User {UserId} has unlimited subscription, no credits consumed", request.UserId);

                    // Get user credits for response (may be null/0 for unlimited users)
                    var unlimitedUserCredits = await _unitOfWork.UserCredits.GetOrCreateAsync(request.UserId, cancellationToken);
                    return SubscriptionDto.FromEntityWithCredits(subscription, unlimitedUserCredits);
                }

                // Get or create UserCreditsEntity for this user
                var userCredits = await _unitOfWork.UserCredits.GetOrCreateAsync(request.UserId, cancellationToken);

                // Store the version before modification
                var expectedVersion = userCredits.Version;

                // For limited/no subscriptions: check limits and validate sufficient credits
                var limitCheck = await _creditLimitService.CheckLimitsAsync(
                    request.UserId,
                    request.Amount,
                    request.ResourceType,
                    cancellationToken);

                if (!limitCheck.IsWithinLimits)
                {
                    var violation = limitCheck.Violations?.FirstOrDefault();

                    // Audit the limit exceeded event
                    await _auditService.LogFailureAsync(
                        AuditActionTypes.CreditLimitExceeded,
                        nameof(UserCreditsEntity),
                        userCredits.Id.ToString(),
                        $"Credit limit exceeded: {violation?.LimitType} limit",
                        new Dictionary<string, object>
                        {
                            ["requestedAmount"] = request.Amount,
                            ["limitType"] = violation?.LimitType ?? "unknown",
                            ["maxCredits"] = violation?.MaxCredits ?? 0,
                            ["consumedCredits"] = violation?.ConsumedCredits ?? 0,
                            ["resourceType"] = request.ResourceType ?? "general"
                        },
                        cancellationToken);

                    throw new CreditLimitExceededException(
                        $"Credit limit exceeded: {violation?.LimitType} limit. " +
                        $"Max: {violation?.MaxCredits}, Used: {violation?.ConsumedCredits}, " +
                        $"Remaining: {violation?.RemainingCredits}");
                }

                // Consume credits via domain logic (throws InsufficientCreditsException if not enough)
                userCredits.ConsumeCredits(request.Amount);

                // Update with version check
                await _unitOfWork.UserCredits.UpdateWithVersionCheckAsync(userCredits, expectedVersion, cancellationToken);

                // Update credit limits INSIDE the transaction
                await _creditLimitService.ConsumeLimitsAsync(
                    request.UserId,
                    request.Amount,
                    request.ResourceType,
                    cancellationToken);

                // Save all changes within the transaction
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // Commit transaction
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                // Publish event after successful transaction
                await _events.PublishAsync(new CreditsConsumedEvent(
                    userCredits.Id,
                    userCredits.UserId,
                    request.Amount,
                    request.ResourceType,
                    request.ResourceName));

                // Audit successful credit consumption
                await _auditService.LogSuccessAsync(
                    AuditActionTypes.CreditConsume,
                    nameof(UserCreditsEntity),
                    userCredits.Id.ToString(),
                    oldValues: new { AvailableCredits = userCredits.AvailableCredits + request.Amount },
                    newValues: new { AvailableCredits = userCredits.AvailableCredits },
                    metadata: new Dictionary<string, object>
                    {
                        ["amount"] = request.Amount,
                        ["resourceType"] = request.ResourceType ?? "general",
                        ["resourceName"] = request.ResourceName ?? "unknown",
                        ["hasSubscription"] = subscription != null
                    },
                    cancellationToken);

                _logger.LogInformation("Successfully consumed {Amount} credits for user {UserId}", request.Amount, request.UserId);

                return SubscriptionDto.FromEntityWithCredits(subscription, userCredits);
            }
            catch (ConcurrencyException ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                retryCount++;

                if (retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogError(ex, "Max retry attempts reached for consuming credits for user {UserId}", request.UserId);
                    throw;
                }

                _logger.LogWarning("Concurrency conflict detected, retrying... Attempt {RetryCount} of {MaxAttempts}",
                    retryCount, MAX_RETRY_ATTEMPTS);

                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount - 1)), cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }

        throw new OperationFailedException("consume credits");
    }
}
