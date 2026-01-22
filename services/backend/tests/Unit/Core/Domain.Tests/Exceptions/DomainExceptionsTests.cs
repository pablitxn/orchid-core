using Domain.Exceptions;

namespace Domain.Tests.Exceptions;

public class EntityNotFoundExceptionTests
{
    [Fact]
    public void DefaultConstructor_CreatesException()
    {
        // Arrange & Act
        var exception = new EntityNotFoundException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<EntityNotFoundException>(exception);
    }

    [Fact]
    public void MessageConstructor_SetsMessage()
    {
        // Arrange
        var message = "Entity not found";

        // Act
        var exception = new EntityNotFoundException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void MessageAndInnerExceptionConstructor_SetsBoth()
    {
        // Arrange
        var message = "Entity not found";
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new EntityNotFoundException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(innerException, exception.InnerException);
    }

    [Fact]
    public void EntityNameAndIdConstructor_FormatsMessage()
    {
        // Arrange
        var entityName = "User";
        var id = Guid.NewGuid();

        // Act
        var exception = new EntityNotFoundException(entityName, id);

        // Assert
        Assert.Contains(entityName, exception.Message);
        Assert.Contains(id.ToString(), exception.Message);
        Assert.Equal($"Entity '{entityName}' with id '{id}' was not found.", exception.Message);
    }

    [Fact]
    public void InheritsFromException()
    {
        // Arrange & Act
        var exception = new EntityNotFoundException();

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }
}

public class CreditLimitExceededExceptionTests
{
    [Fact]
    public void Constructor_SetsMessage()
    {
        // Arrange
        var message = "Credit limit exceeded: 100 credits required, 50 available";

        // Act
        var exception = new CreditLimitExceededException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void InheritsFromException()
    {
        // Arrange & Act
        var exception = new CreditLimitExceededException("Test");

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }
}

public class ConcurrencyExceptionTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        // Arrange
        var entityType = "Subscription";
        var entityId = Guid.NewGuid();
        var expectedVersion = 5;
        var actualVersion = 3;

        // Act
        var exception = new ConcurrencyException(entityType, entityId, expectedVersion, actualVersion);

        // Assert
        Assert.Equal(entityType, exception.EntityType);
        Assert.Equal(entityId, exception.EntityId);
        Assert.Equal(expectedVersion, exception.ExpectedVersion);
        Assert.Equal(actualVersion, exception.ActualVersion);
    }

    [Fact]
    public void Constructor_FormatsMessage()
    {
        // Arrange
        var entityType = "Subscription";
        var entityId = Guid.NewGuid();
        var expectedVersion = 5;
        var actualVersion = 3;

        // Act
        var exception = new ConcurrencyException(entityType, entityId, expectedVersion, actualVersion);

        // Assert
        Assert.Contains(entityType, exception.Message);
        Assert.Contains(entityId.ToString(), exception.Message);
        Assert.Contains(expectedVersion.ToString(), exception.Message);
        Assert.Contains(actualVersion.ToString(), exception.Message);
        Assert.Equal(
            $"Concurrency conflict on {entityType} with ID {entityId}. Expected version {expectedVersion}, but found {actualVersion}.",
            exception.Message);
    }

    [Fact]
    public void InheritsFromException()
    {
        // Arrange & Act
        var exception = new ConcurrencyException("Test", Guid.NewGuid(), 1, 2);

        // Assert
        Assert.IsAssignableFrom<Exception>(exception);
    }
}
