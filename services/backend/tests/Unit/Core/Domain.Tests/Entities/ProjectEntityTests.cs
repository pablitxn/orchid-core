using Domain.Entities;
using Domain.Events;

namespace Domain.Tests.Entities;

public class ProjectEntityTests
{
    [Fact]
    public void Create_ShouldInitializeAndAddEvent()
    {
        // Arrange
        var audio = new AudioFileEntity
        {
            FileName = "foo.mp3",
            ContentType = "audio/mpeg",
            FileSize = 1024
        };

        // Act
        var project = ProjectEntity.Create("en", audio);

        // Assert
        Assert.Equal("New Project", project.Name);
        var @event = Assert.Single(project.DomainEvents);
        Assert.IsType<ProjectCreatedEvent>(@event);
    }

    [Fact]
    public void ClearDomainEvents_ShouldRemoveAllEvents()
    {
        // Arrange
        var audio = new AudioFileEntity
        {
            FileName = "foo.mp3",
            ContentType = "audio/mpeg",
            FileSize = 1024
        };
        var project = ProjectEntity.Create("en", audio);

        // Act
        project.ClearDomainEvents();

        // Assert
        Assert.Empty(project.DomainEvents);
    }
}