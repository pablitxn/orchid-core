using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.AddCredits;

public record AddCreditsCommand(Guid UserId, int Amount) : IRequest<SubscriptionEntity>;