using Application.Interfaces;
using MediatR;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Application.UseCases.Subscription.ConsumeCredits;

namespace Application.UseCases.Workflow.PurchaseWorkflow;

public class PurchaseWorkflowHandler(
    IUnitOfWork unitOfWork,
    ICreditTrackingService creditTrackingService,
    ICostRegistry costRegistry,
    ICreditLimitService creditLimitService,
    ILogger<PurchaseWorkflowHandler> logger)
    : IRequestHandler<PurchaseWorkflowCommand, PurchaseResult>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICreditTrackingService _creditTracking = creditTrackingService;
    private readonly ICostRegistry _costRegistry = costRegistry;
    private readonly ICreditLimitService _creditLimitService = creditLimitService;
    private readonly ILogger<PurchaseWorkflowHandler> _logger = logger;

    public async Task<PurchaseResult> Handle(PurchaseWorkflowCommand request, CancellationToken cancellationToken)
    {
        const int MAX_RETRY_ATTEMPTS = 3;
        int retryCount = 0;
        
        while (retryCount < MAX_RETRY_ATTEMPTS)
        {
            try
            {
                await _unitOfWork.BeginTransactionAsync(cancellationToken);
                
                var workflow = await _unitOfWork.Workflows.GetByIdAsync(request.WorkflowId, cancellationToken);
                if (workflow is null)
                {
                    _logger.LogWarning("Workflow not found: {WorkflowId}", request.WorkflowId);
                    throw new KeyNotFoundException($"Workflow with ID {request.WorkflowId} was not found.");
                }

                var subscription = await _unitOfWork.Subscriptions.GetByUserIdAsync(request.UserId, cancellationToken);
                if (subscription is null)
                {
                    _logger.LogWarning("User subscription not found: {UserId}", request.UserId);
                    throw new InvalidOperationException("You must have an active subscription to purchase workflows.");
                }
                
                // Store the version before modification
                var expectedVersion = subscription.Version;

                // Check if already purchased
                if (await _unitOfWork.Workflows.UserHasWorkflowAsync(request.UserId, request.WorkflowId, cancellationToken))
                {
                    _logger.LogWarning("User {UserId} already purchased workflow {WorkflowId}", request.UserId, request.WorkflowId);
                    throw new InvalidOperationException("You have already purchased this workflow.");
                }
                
                // Get the actual cost from the registry
                var purchaseCost = await _costRegistry.GetWorkflowPurchaseCostAsync(workflow.Id, cancellationToken);
                
                if (!subscription.HasUnlimitedCredits())
                {
                    // Check credit limits
                    var limitCheck = await _creditLimitService.CheckLimitsAsync(
                        request.UserId,
                        purchaseCost,
                        "workflow_purchase",
                        cancellationToken);
                    
                    if (!limitCheck.IsWithinLimits)
                    {
                        throw new CreditLimitExceededException(
                            $"Credit limit exceeded for workflow purchase. Required: {purchaseCost}, " +
                            $"Limit: {limitCheck.Violations?.FirstOrDefault()?.MaxCredits}");
                    }
                    
                    if (subscription.Credits < purchaseCost)
                    {
                        _logger.LogWarning("Insufficient credits. User {UserId} has {Credits} but needs {Required}", 
                            request.UserId, subscription.Credits, purchaseCost);
                        throw new InsufficientCreditsException(subscription.Credits, purchaseCost);
                    }
                }

                // Deduct credits
                subscription.ConsumeCredits(purchaseCost);
                
                // Update with version check
                await _unitOfWork.Subscriptions.UpdateWithVersionCheckAsync(subscription, expectedVersion, cancellationToken);
                
                // Record purchase
                await _unitOfWork.Workflows.AddUserWorkflowAsync(new UserWorkflowEntity
                {
                    UserId = request.UserId,
                    WorkflowId = request.WorkflowId,
                    PurchasedAt = DateTime.UtcNow
                }, cancellationToken);
                
                // Update credit limits INSIDE the transaction (only for limited subscriptions)
                if (!subscription.HasUnlimitedCredits())
                {
                    await _creditLimitService.ConsumeLimitsAsync(
                        request.UserId,
                        purchaseCost,
                        "workflow_purchase",
                        cancellationToken);
                }
                
                // Save all changes within the transaction
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                
                // Commit transaction
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                
                // Track credit consumption after successful transaction
                await _creditTracking.TrackWorkflowUsageAsync(
                    request.UserId,
                    workflow.Id,
                    workflow.Name,
                    purchaseCost,
                    JsonSerializer.Serialize(new { action = "purchase", workflowId = workflow.Id, workflowName = workflow.Name }),
                    cancellationToken);
                
                _logger.LogInformation("User {UserId} successfully purchased workflow {WorkflowId} for {Credits} credits", 
                    request.UserId, request.WorkflowId, purchaseCost);
                    
                return PurchaseResult.Success;
            }
            catch (ConcurrencyException ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                retryCount++;
                
                if (retryCount >= MAX_RETRY_ATTEMPTS)
                {
                    _logger.LogError(ex, "Max retry attempts reached for purchasing workflow {WorkflowId} for user {UserId}", 
                        request.WorkflowId, request.UserId);
                    throw new InvalidOperationException("Unable to complete workflow purchase due to concurrent updates. Please try again.", ex);
                }
                
                _logger.LogWarning("Concurrency conflict detected, retrying... Attempt {RetryCount} of {MaxAttempts}", 
                    retryCount, MAX_RETRY_ATTEMPTS);
                
                // Exponential backoff
                await Task.Delay(TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryCount - 1)), cancellationToken);
            }
            catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or 
                                       InsufficientCreditsException or CreditLimitExceededException)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                _logger.LogError(ex, "Unexpected error purchasing workflow {WorkflowId} for user {UserId}", 
                    request.WorkflowId, request.UserId);
                throw new InvalidOperationException("An error occurred while processing your workflow purchase.", ex);
            }
        }
        
        throw new InvalidOperationException("Failed to purchase workflow after maximum retry attempts");
    }
}