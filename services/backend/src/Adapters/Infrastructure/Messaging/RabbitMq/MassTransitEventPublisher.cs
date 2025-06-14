using Application.Interfaces;
using Domain.Common;
using MassTransit;

namespace Infrastructure.Messaging;

/// <summary>
///     Publishes domain events to the message bus using MassTransit.
/// </summary>
public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    /// <inheritdoc />
    public async Task PublishAsync<T>(T domainEvent) where T : IDomainEvent
    {
        await _publishEndpoint.Publish(domainEvent);
    }
}