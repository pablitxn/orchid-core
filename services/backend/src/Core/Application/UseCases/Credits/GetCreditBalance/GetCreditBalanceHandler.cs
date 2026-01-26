using Application.Interfaces;
using Application.UseCases.Credits.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Credits.GetCreditBalance;

public sealed class GetCreditBalanceHandler(
    ISubscriptionRepository subscriptionRepository,
    IFreeTierCreditService freeTierCreditService)
    : IRequestHandler<GetCreditBalanceQuery, CreditBalanceDto>
{
    public async Task<CreditBalanceDto> Handle(
        GetCreditBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (subscription == null)
        {
            // Check free tier eligibility
            var isEligible = await freeTierCreditService.IsEligibleForFreeTierAsync(request.UserId, cancellationToken);
            var remaining = isEligible
                ? await freeTierCreditService.GetRemainingDailyCreditsAsync(request.UserId, cancellationToken)
                : 0;

            return new CreditBalanceDto
            {
                Balance = remaining,
                IsUnlimited = false,
                ExpiresAt = null,
                UpdatedAt = DateTime.UtcNow,
                HasSubscription = false,
                FreeTierCreditsRemaining = isEligible ? remaining : null
            };
        }

        var isUnlimited = subscription.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;

        return new CreditBalanceDto
        {
            Balance = subscription.Credits,
            IsUnlimited = isUnlimited,
            ExpiresAt = subscription.ExpiresAt,
            UpdatedAt = subscription.UpdatedAt,
            HasSubscription = true,
            FreeTierCreditsRemaining = null
        };
    }
}
