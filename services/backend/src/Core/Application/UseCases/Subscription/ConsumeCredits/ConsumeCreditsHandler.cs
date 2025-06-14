using Application.Interfaces;
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
    : IRequestHandler<ConsumeCreditsCommand, SubscriptionEntity>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IEventPublisher _events = eventPublisher;
    private readonly ICreditLimitService _creditLimitService = creditLimitService;
    private readonly IAuditService _auditService = auditService;
    private readonly ILogger<ConsumeCreditsHandler> _logger = logger;

    public async Task<SubscriptionEntity> Handle(ConsumeCreditsCommand request, CancellationToken cancellationToken)
    {
        const int MAX_RETRY_ATTEMPTS = 3;
        int retryCount = 0;
        
        while (retryCount < MAX_RETRY_ATTEMPTS)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                
                var subscription = await _unitOfWork.Subscriptions.GetByUserIdAsync(request.UserId, cancellationToken);
                if (subscription == null)
                    throw new SubscriptionNotFoundException(request.UserId);

                // Store the version before modification
                var expectedVersion = subscription.Version;

                // Handle credit-limited vs unlimited subscriptions
                if (!subscription.HasUnlimitedCredits())
                {
                    // For limited subscriptions: check limits and validate sufficient credits
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
                            nameof(SubscriptionEntity),
                            subscription.Id.ToString(),
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

                    // Ensure the user has enough credits before consuming
                    if (subscription.Credits < request.Amount)
                        throw new InsufficientCreditsException(subscription.Credits, request.Amount);
                }

                // Consume credits via domain logic (handles unlimited plans internally)
                subscription.ConsumeCredits(request.Amount);

                // Update with version check
                await _unitOfWork.Subscriptions.UpdateWithVersionCheckAsync(subscription, expectedVersion, cancellationToken);
                
                // Update credit limits INSIDE the transaction (only for limited subscriptions)
                if (!subscription.HasUnlimitedCredits())
                {
                    await _creditLimitService.ConsumeLimitsAsync(
                        request.UserId, 
                        request.Amount, 
                        request.ResourceType, 
                        cancellationToken);
                }
                
                // Save all changes within the transaction
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                // Commit transaction
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                
                // Publish event after successful transaction
                await _events.PublishAsync(new CreditsConsumedEvent(
                    subscription.Id, 
                    subscription.UserId, 
                    request.Amount,
                    request.ResourceType,
                    request.ResourceName));
                
                // Audit successful credit consumption
                await _auditService.LogSuccessAsync(
                    AuditActionTypes.CreditConsume,
                    nameof(SubscriptionEntity),
                    subscription.Id.ToString(),
                    oldValues: new { Credits = subscription.Credits + request.Amount },
                    newValues: new { Credits = subscription.Credits },
                    metadata: new Dictionary<string, object>
                    {
                        ["amount"] = request.Amount,
                        ["resourceType"] = request.ResourceType ?? "general",
                        ["resourceName"] = request.ResourceName ?? "unknown",
                        ["hasUnlimitedCredits"] = subscription.HasUnlimitedCredits()
                    },
                    cancellationToken);
                
                _logger.LogInformation("Successfully consumed {Amount} credits for user {UserId}", request.Amount, request.UserId);
                
                return subscription;
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
        
        throw new InvalidOperationException("Failed to consume credits after maximum retry attempts");
    }
}