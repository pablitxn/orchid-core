using Application.Interfaces;
using Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.UseCases.KnowledgeBase.SeedDefaultFiles;

/// <summary>
/// Handles UserCreatedFromAuth0Event by publishing UserCreatedEvent
/// to reuse existing onboarding logic (welcome credits, plugin assignment).
/// </summary>
public class UserCreatedFromAuth0EventHandler(
    IEventPublisher eventPublisher,
    ILogger<UserCreatedFromAuth0EventHandler> logger)
    : INotificationHandler<UserCreatedFromAuth0Event>
{
    public async Task Handle(UserCreatedFromAuth0Event notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Processing Auth0 user creation for {UserId}, delegating to standard onboarding flow",
            notification.UserId);

        // Publish UserCreatedEvent to trigger standard onboarding (welcome credits, plugins)
        await eventPublisher.PublishAsync(
            new UserCreatedEvent(notification.UserId, notification.Email),
            cancellationToken);
    }
}
