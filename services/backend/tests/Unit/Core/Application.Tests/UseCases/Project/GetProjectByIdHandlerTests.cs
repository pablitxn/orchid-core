using Application.Interfaces;
using Application.UseCases.Project.GetProjectById;
using Domain.Entities;
using Moq;
using Xunit;

namespace Application.Tests.UseCases.Project;

public class GetProjectByIdHandlerTests
{
    private readonly Mock<IProjectRepository> _projectRepositoryMock;
    private readonly GetProjectByIdHandler _handler;

    public GetProjectByIdHandlerTests()
    {
        _projectRepositoryMock = new Mock<IProjectRepository>();
        _handler = new GetProjectByIdHandler(_projectRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsProjectDto_WhenProjectExists()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var query = new GetProjectByIdQuery(projectId);

        var audioFile = new AudioFileEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test-audio.mp3",
            ContentType = "audio/mp3",
            FileSize = 1024,
            Url = "storage/test.mp3"
        };
        var project = ProjectEntity.Create("en", audioFile);

        _projectRepositoryMock
            .Setup(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("en", result.TargetLanguage);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenProjectNotFound()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var query = new GetProjectByIdQuery(projectId);

        _projectRepositoryMock
            .Setup(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectEntity?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Handle_CallsRepository_WithCorrectProjectId()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var query = new GetProjectByIdQuery(projectId);

        _projectRepositoryMock
            .Setup(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectEntity?)null);

        // Act
        await _handler.Handle(query, CancellationToken.None);

        // Assert
        _projectRepositoryMock.Verify(x => x.GetByIdAsync(projectId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
