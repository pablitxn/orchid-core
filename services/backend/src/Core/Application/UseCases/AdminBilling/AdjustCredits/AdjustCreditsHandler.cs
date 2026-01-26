using Application.Interfaces;
using Application.UseCases.AdminBilling.Common;
using Domain.Entities;
using Domain.Events;
using Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.AdminBilling.AdjustCredits;

public class AdjustCreditsHandler(
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

        var subscription = await subscriptionRepository.GetByUserIdWithPlanAsync(
            request.UserId,
            cancellationToken);

        if (subscription is null)
            throw new SubscriptionNotFoundException(request.UserId);

        var oldCredits = subscription.Credits;

        // Handle both positive (add) and negative (remove) adjustments
        if (request.Amount > 0)
        {
            subscription.AddCredits(request.Amount);
        }
        else
        {
            var absAmount = Math.Abs(request.Amount);
            if (subscription.Credits < absAmount)
                throw new InsufficientCreditsException(subscription.Credits, absAmount);

            // Direct credit reduction for admin adjustment
            subscription.Credits -= absAmount;
            subscription.UpdatedAt = DateTime.UtcNow;
            subscription.Version++;
        }

        await subscriptionRepository.UpdateAsync(subscription, cancellationToken);

        // Audit the action
        await auditService.LogSuccessAsync(
            AuditActionTypes.AdminCreditAdjust,
            nameof(SubscriptionEntity),
            subscription.Id.ToString(),
            oldValues: new { Credits = oldCredits },
            newValues: new { Credits = subscription.Credits, Amount = request.Amount },
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
                new CreditsAddedEvent(subscription.Id, subscription.UserId, request.Amount));
        }
        else
        {
            await eventPublisher.PublishAsync(
                new CreditsConsumedEvent(subscription.Id, subscription.UserId, Math.Abs(request.Amount)));
        }

        logger.LogInformation(
            "Admin {AdminUserId} successfully adjusted credits for user {UserId}. Old: {OldCredits}, New: {NewCredits}",
            request.AdminUserId, request.UserId, oldCredits, subscription.Credits);

        // Reload with user info for response
        subscription = await subscriptionRepository.GetByUserIdWithPlanAsync(
            request.UserId,
            cancellationToken);

        return AdminSubscriptionDto.FromEntity(subscription!);
    }
}
