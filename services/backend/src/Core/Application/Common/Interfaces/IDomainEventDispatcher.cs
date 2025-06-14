using Domain.Common;

namespace Application.Common.Interfaces;

public interface IDomainEventDispatcher
{
    Task DispatchAndClearEventsAsync(IEnumerable<EntityBase> entitiesWithEvents);
}