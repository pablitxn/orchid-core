using Domain.Common;
using Domain.Events;

namespace Domain.Tests.Events;

public class ProjectCreatedEventTests
{
    [Fact]
    public void Constructor_SetsProjectId()
    {
        // Arrange
        var projectId = Guid.NewGuid();

        // Act
        var @event = new ProjectCreatedEvent(projectId);

        // Assert
        Assert.Equal(projectId, @event.ProjectId);
    }

    [Fact]
    public void OccurredOn_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new ProjectCreatedEvent(Guid.NewGuid());

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new ProjectCreatedEvent(Guid.NewGuid());

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}

public class SubscriptionCreatedEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var @event = new SubscriptionCreatedEvent(subscriptionId, userId);

        // Assert
        Assert.Equal(subscriptionId, @event.SubscriptionId);
        Assert.Equal(userId, @event.UserId);
    }

    [Fact]
    public void OccurredOn_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new SubscriptionCreatedEvent(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new SubscriptionCreatedEvent(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}

public class SubscriptionUpdatedEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var @event = new SubscriptionUpdatedEvent(subscriptionId, userId);

        // Assert
        Assert.Equal(subscriptionId, @event.SubscriptionId);
        Assert.Equal(userId, @event.UserId);
    }

    [Fact]
    public void OccurredOn_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new SubscriptionUpdatedEvent(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new SubscriptionUpdatedEvent(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}

public class UserCreatedEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";

        // Act
        var @event = new UserCreatedEvent(userId, email);

        // Assert
        Assert.Equal(userId, @event.UserId);
        Assert.Equal(email, @event.Email);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new UserCreatedEvent(Guid.NewGuid(), "test@example.com");

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}

public class CreditsAddedEventTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var amount = 100;

        // Act
        var @event = new CreditsAddedEvent(subscriptionId, userId, amount);

        // Assert
        Assert.Equal(subscriptionId, @event.SubscriptionId);
        Assert.Equal(userId, @event.UserId);
        Assert.Equal(amount, @event.Amount);
    }

    [Fact]
    public void OccurredOn_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new CreditsAddedEvent(Guid.NewGuid(), Guid.NewGuid(), 50);

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new CreditsAddedEvent(Guid.NewGuid(), Guid.NewGuid(), 100);

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}

public class CreditsConsumedEventTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var amount = 25;

        // Act
        var @event = new CreditsConsumedEvent(subscriptionId, userId, amount);

        // Assert
        Assert.Equal(subscriptionId, @event.SubscriptionId);
        Assert.Equal(userId, @event.UserId);
        Assert.Equal(amount, @event.Amount);
        Assert.Null(@event.ResourceType);
        Assert.Null(@event.ResourceName);
    }

    [Fact]
    public void Constructor_SetsOptionalProperties()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var amount = 10;
        var resourceType = "plugin";
        var resourceName = "Web Search";

        // Act
        var @event = new CreditsConsumedEvent(subscriptionId, userId, amount, resourceType, resourceName);

        // Assert
        Assert.Equal(subscriptionId, @event.SubscriptionId);
        Assert.Equal(userId, @event.UserId);
        Assert.Equal(amount, @event.Amount);
        Assert.Equal(resourceType, @event.ResourceType);
        Assert.Equal(resourceName, @event.ResourceName);
    }

    [Fact]
    public void OccurredOn_IsSetToUtcNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var @event = new CreditsConsumedEvent(Guid.NewGuid(), Guid.NewGuid(), 10);

        // Assert
        var after = DateTime.UtcNow;
        Assert.True(@event.OccurredOn >= before && @event.OccurredOn <= after);
    }

    [Fact]
    public void ImplementsIDomainEvent()
    {
        // Arrange & Act
        var @event = new CreditsConsumedEvent(Guid.NewGuid(), Guid.NewGuid(), 5);

        // Assert
        Assert.IsAssignableFrom<IDomainEvent>(@event);
    }
}
