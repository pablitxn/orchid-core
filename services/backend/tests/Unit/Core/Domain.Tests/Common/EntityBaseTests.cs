using Domain.Common;

namespace Domain.Tests.Common;

public class EntityBaseTests
{
    private class TestEntity : EntityBase
    {
        public TestEntity()
        {
            Id = Guid.NewGuid();
        }

        public void AddTestEvent(IDomainEvent domainEvent)
        {
            AddDomainEvent(domainEvent);
        }
    }

    private record TestDomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    [Fact]
    public void DomainEvents_IsEmptyByDefault()
    {
        // Arrange
        var entity = new TestEntity();

        // Assert
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void AddDomainEvent_AddsEventToCollection()
    {
        // Arrange
        var entity = new TestEntity();
        var @event = new TestDomainEvent();

        // Act
        entity.AddTestEvent(@event);

        // Assert
        Assert.Single(entity.DomainEvents);
        Assert.Contains(@event, entity.DomainEvents);
    }

    [Fact]
    public void AddDomainEvent_CanAddMultipleEvents()
    {
        // Arrange
        var entity = new TestEntity();
        var event1 = new TestDomainEvent();
        var event2 = new TestDomainEvent();
        var event3 = new TestDomainEvent();

        // Act
        entity.AddTestEvent(event1);
        entity.AddTestEvent(event2);
        entity.AddTestEvent(event3);

        // Assert
        Assert.Equal(3, entity.DomainEvents.Count);
    }

    [Fact]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        // Arrange
        var entity = new TestEntity();
        entity.AddTestEvent(new TestDomainEvent());
        entity.AddTestEvent(new TestDomainEvent());

        // Act
        entity.ClearDomainEvents();

        // Assert
        Assert.Empty(entity.DomainEvents);
    }

    [Fact]
    public void DomainEvents_ReturnsReadOnlyCollection()
    {
        // Arrange
        var entity = new TestEntity();

        // Assert
        Assert.IsAssignableFrom<IReadOnlyCollection<IDomainEvent>>(entity.DomainEvents);
    }

    [Fact]
    public void Id_CanBeSet()
    {
        // Arrange
        var entity = new TestEntity();

        // Assert
        Assert.NotEqual(Guid.Empty, entity.Id);
    }
}

public class IDomainEventTests
{
    private record TestDomainEvent : IDomainEvent
    {
        public DateTime OccurredOn { get; } = DateTime.UtcNow;
    }

    [Fact]
    public void OccurredOn_ReturnsDateTime()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new TestDomainEvent();

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }
}
