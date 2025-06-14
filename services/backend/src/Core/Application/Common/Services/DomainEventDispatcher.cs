using Application.Common.Interfaces;
using Domain.Common;
using MediatR;

namespace Application.Common.Services;

public class DomainEventDispatcher(IMediator mediator) : IDomainEventDispatcher
{
    private readonly IMediator _mediator = mediator;

    public async Task DispatchAndClearEventsAsync(IEnumerable<EntityBase> entitiesWithEvents)
    {
        foreach (var entity in entitiesWithEvents)
        {
            var events = entity.DomainEvents.ToArray();
            entity.ClearDomainEvents();

            foreach (var domainEvent in events) await _mediator.Publish(domainEvent);
        }
    }
}