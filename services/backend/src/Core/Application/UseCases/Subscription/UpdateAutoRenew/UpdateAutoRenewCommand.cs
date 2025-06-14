using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.UpdateAutoRenew;

public record UpdateAutoRenewCommand(Guid UserId, bool AutoRenew) : IRequest<SubscriptionEntity>;