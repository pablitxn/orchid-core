using Application.UseCases.Subscription.Common;
using MediatR;

namespace Application.UseCases.Subscription.UpdateAutoRenew;

public record UpdateAutoRenewCommand(Guid UserId, bool AutoRenew) : IRequest<SubscriptionDto>;