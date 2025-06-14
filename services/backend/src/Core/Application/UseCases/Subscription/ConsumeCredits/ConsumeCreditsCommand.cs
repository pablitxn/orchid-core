using Domain.Entities;
using MediatR;

namespace Application.UseCases.Subscription.ConsumeCredits;

public record ConsumeCreditsCommand(
    Guid UserId, 
    int Amount, 
    string? ResourceType = null, 
    string? ResourceName = null) : IRequest<SubscriptionEntity>;