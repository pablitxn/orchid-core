using Domain.Common;

namespace Application.Interfaces;

public interface IEventPublisher
{
    Task PublishAsync<T>(T domainEvent) where T : IDomainEvent;
}