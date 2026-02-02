using Application.Interfaces;
using Application.UseCases.Credits.Common;
using Domain.Enums;
using MediatR;

namespace Application.UseCases.Credits.GetCreditBalance;

public sealed class GetCreditBalanceHandler(
    IUserCreditsRepository userCreditsRepository,
    ISubscriptionRepository subscriptionRepository,
    IFreeTierCreditService freeTierCreditService)
    : IRequestHandler<GetCreditBalanceQuery, CreditBalanceDto>
{
    public async Task<CreditBalanceDto> Handle(
        GetCreditBalanceQuery request,
        CancellationToken cancellationToken)
    {
        // Get user credits
        var userCredits = await userCreditsRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        // Get subscription for plan info
        var subscription = await subscriptionRepository.GetByUserIdAsync(request.UserId, cancellationToken);

        if (userCredits == null && subscription == null)
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

        var isUnlimited = subscription?.SubscriptionPlan?.PlanEnum == SubscriptionPlanEnum.Unlimited;
        var balance = userCredits?.AvailableCredits ?? 0;

        return new CreditBalanceDto
        {
            Balance = balance,
            IsUnlimited = isUnlimited,
            ExpiresAt = subscription?.ExpiresAt,
            UpdatedAt = userCredits?.UpdatedAt ?? subscription?.UpdatedAt ?? DateTime.UtcNow,
            HasSubscription = subscription != null,
            FreeTierCreditsRemaining = null
        };
    }
}
