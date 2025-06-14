using Application.Interfaces;
using Application.UseCases.Project.CreateProject;
using Domain.Entities;
using Domain.Events;
using Moq;

namespace Application.Tests.UseCases.Project.CreateProject;

public class CreateProjectByAudioHandlerTests
{
    private readonly Mock<IEventPublisher> _eventPublisherMock;
    private readonly CreateProjectByAudioHandler _handler;
    private readonly Mock<IProjectRepository> _projectRepositoryMock;
    private readonly DateTime _before;

    public CreateProjectByAudioHandlerTests()
    {
        _projectRepositoryMock = new Mock<IProjectRepository>();
        _eventPublisherMock = new Mock<IEventPublisher>();
        _handler = new CreateProjectByAudioHandler(_projectRepositoryMock.Object, _eventPublisherMock.Object);
        _before = DateTime.UtcNow;
    }

    [Fact]
    public async Task Handle_CreatesProjectAndSavesIt()
    {
        // Arrange
        var command = new CreateProjectByAudioCommand(
            targetLanguage: "en",
            fileName: "test.mp3",
            contentType: "audio/mpeg",
            fileSize: 1024,
            url: "testUrl"
        );

        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        _projectRepositoryMock.Verify(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.NotNull(result);
        Assert.Equal("en", result.TargetLanguage);
        Assert.NotNull(result.AudioFile);
        Assert.Equal("test.mp3", result.AudioFile.FileName);
    }

    [Fact]
    public async Task Handle_AssignsAudioFileToProject()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal(result, result.AudioFile!.Project);
    }

    [Fact]
    public async Task Handle_PublishesEvent()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _handler.Handle(cmd, CancellationToken.None);

        _eventPublisherMock.Verify(p => p.PublishAsync(It.IsAny<ProjectCreatedEvent>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SetsCreatedAt()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(result.AudioFile!.CreatedAt >= _before);
    }

    [Fact]
    public async Task Handle_SavesProject()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        var saved = false;
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Callback(() => saved = true)
            .Returns(Task.CompletedTask);

        await _handler.Handle(cmd, CancellationToken.None);

        Assert.True(saved);
    }

    [Fact]
    public async Task Handle_ReturnsProjectWithId()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task Handle_SetsAudioFileProperties()
    {
        var cmd = new CreateProjectByAudioCommand("en", "f.mp3", "audio/mpeg", 5, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("f.mp3", result.AudioFile!.FileName);
        Assert.Equal("audio/mpeg", result.AudioFile.ContentType);
        Assert.Equal(5, result.AudioFile.FileSize);
        Assert.Equal("url", result.AudioFile.Url);
    }

    [Fact]
    public async Task Handle_ReturnsProjectWithTargetLanguage()
    {
        var cmd = new CreateProjectByAudioCommand("es", "f.mp3", "audio/mpeg", 5, "url");
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _handler.Handle(cmd, CancellationToken.None);

        Assert.Equal("es", result.TargetLanguage);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        var tokenSource = new CancellationTokenSource();
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), tokenSource.Token))
            .Returns(Task.CompletedTask)
            .Verifiable();

        await _handler.Handle(cmd, tokenSource.Token);

        _projectRepositoryMock.Verify();
    }

    [Fact]
    public async Task Handle_PublishesEventWithProjectId()
    {
        var cmd = new CreateProjectByAudioCommand("en", "file.mp3", "audio/mpeg", 1, "url");
        ProjectEntity? savedProject = null;
        _projectRepositoryMock.Setup(r => r.SaveAsync(It.IsAny<ProjectEntity>(), It.IsAny<CancellationToken>()))
            .Callback<ProjectEntity, CancellationToken>((p, _) => savedProject = p)
            .Returns(Task.CompletedTask);

        await _handler.Handle(cmd, CancellationToken.None);

        _eventPublisherMock.Verify(p => p.PublishAsync(It.Is<ProjectCreatedEvent>(e => e.ProjectId == savedProject!.Id)), Times.Once);
    }
}