using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.AdjustCredits;

public class AdjustCreditsHandler(
    IUserCreditsRepository userCreditsRepository,
    ISubscriptionRepository subscriptionRepository,
    IAuditService auditService,
    IEventPublisher eventPublisher,
    ILogger<AdjustCreditsHandler> logger)
    : IRequestHandler<AdjustCreditsCommand, AdminSubscriptionDto>
{
    public async Task<AdminSubscriptionDto> Handle(
        AdjustCreditsCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ValidationException("Reason is required for credit adjustments.", nameof(request.Reason));

        if (request.Amount == 0)
            throw new ValidationException("Amount cannot be zero.", nameof(request.Amount));

        logger.LogInformation(
            "Admin {AdminUserId} adjusting credits for user {UserId}. Amount: {Amount}, Reason: {Reason}",
            request.AdminUserId, request.UserId, request.Amount, request.Reason);

        // Get or create UserCreditsEntity
        var userCredits = await userCreditsRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (userCredits == null)
        {
            userCredits = new UserCreditsEntity
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                AvailableCredits = 0,
                TotalCreditsGranted = 0,
                TotalCreditsConsumed = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await userCreditsRepository.CreateAsync(userCredits, cancellationToken);
        }

        var oldCredits = userCredits.AvailableCredits;

        // Handle both positive (add) and negative (remove) adjustments
        if (request.Amount > 0)
        {
            userCredits.GrantCredits(request.Amount);
        }
        else
        {
            var absAmount = Math.Abs(request.Amount);
            if (userCredits.AvailableCredits < absAmount)
                throw new InsufficientCreditsException(userCredits.AvailableCredits, absAmount);

            // Direct credit reduction for admin adjustment
            userCredits.AvailableCredits -= absAmount;
            userCredits.UpdatedAt = DateTime.UtcNow;
            userCredits.Version++;
        }

        await userCreditsRepository.UpdateAsync(userCredits, cancellationToken);

        // Audit the action
        await auditService.LogSuccessAsync(
            AuditActionTypes.AdminCreditAdjust,
            nameof(UserCreditsEntity),
            userCredits.Id.ToString(),
            oldValues: new { AvailableCredits = oldCredits },
            newValues: new { AvailableCredits = userCredits.AvailableCredits, Amount = request.Amount },
            metadata: new Dictionary<string, object>
            {
                ["AdminUserId"] = request.AdminUserId,
                ["TargetUserId"] = request.UserId,
                ["Reason"] = request.Reason
            },
            cancellationToken);

        // Publish event
        if (request.Amount > 0)
        {
            await eventPublisher.PublishAsync(
                new CreditsAddedEvent(userCredits.Id, userCredits.UserId, request.Amount));
        }
        else
        {
            await eventPublisher.PublishAsync(
                new CreditsConsumedEvent(userCredits.Id, userCredits.UserId, Math.Abs(request.Amount)));
        }

        logger.LogInformation(
            "Admin {AdminUserId} successfully adjusted credits for user {UserId}. Old: {OldCredits}, New: {NewCredits}",
            request.AdminUserId, request.UserId, oldCredits, userCredits.AvailableCredits);

        // Get subscription for response (may be null)
        var subscription = await subscriptionRepository.GetByUserIdWithPlanAsync(request.UserId, cancellationToken);

        return AdminSubscriptionDto.FromEntityWithCredits(subscription, userCredits);
    }
}
