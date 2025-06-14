using System.Text;
using Application.Interfaces;
using Application.UseCases.Project.CreateProject;
using Application.UseCases.Project.GetProjectById;
using Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class ProjectControllerTests
{
    private readonly ProjectController _controller;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IMediator> _mediatorMock;

    public ProjectControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _controller = new ProjectController(_mediatorMock.Object, _fileStorageServiceMock.Object);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenNotMultipart()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext();
        _controller.ControllerContext.HttpContext = new DefaultHttpContext();
        _controller.ControllerContext.HttpContext.Request.ContentType = "application/json";

        // Act
        var result = await _controller.UploadFile("en", null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Request content type must be multipart/form-data.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenNoFileProvided()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data";

        // Act
        var result = await _controller.UploadFile("en", null!);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file provided.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenTargetLanguageIsMissing()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.ControllerContext.HttpContext.Request.ContentType = "multipart/form-data";

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);

        // Act
        var result = await _controller.UploadFile("", fileMock.Object);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Target language is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFile_ReturnsBadRequest_WhenUnsupportedFileType()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data";
        _controller.ControllerContext.HttpContext = context;

        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("file.xyz");
        fileMock.Setup(f => f.ContentType).Returns("application/xyz");

        // Act
        var result = await _controller.UploadFile("en", fileMock.Object);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("The file extension or MIME type is not supported.", badRequestResult.Value);
    }

    [Fact]
    public async Task UploadFile_ReturnsOk_WithProjectId_OnSuccess()
    {
        // Arrange
        _controller.ControllerContext = new ControllerContext();
        var context = new DefaultHttpContext();
        context.Request.ContentType = "multipart/form-data";
        _controller.ControllerContext.HttpContext = context;

        // Setup a valid audio file
        var content = "dummy audio content";
        var targetLanguage = "en";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var fileMock = new FormFile(stream, 0, stream.Length, "audioFile", "test.mp3")
        {
            Headers = new HeaderDictionary(),
            ContentType = "audio/mpeg"
        };

        // Setup file storage service mock to simulate storing file
        _fileStorageServiceMock.Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("storedFileUrl");

        // Setup mediator to return a dummy project with an Id
        var projectId = Guid.NewGuid();
        var dummyAudio = new AudioFileEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.mp3",
            ContentType = "audio/mpeg",
            FileSize = stream.Length,
            Url = "storedFileUrl"
        };
        var dummyProject = ProjectEntity.Create(targetLanguage, dummyAudio);
        typeof(ProjectEntity).GetProperty("Id")?.SetValue(dummyProject, projectId);
        // Assume TargetLanguage and AudioFile properties are set by the domain logic

        _mediatorMock.Setup(m => m.Send(It.IsAny<CreateProjectByAudioCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyProject);

        // Act
        var result = await _controller.UploadFile("en", fileMock);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        dynamic response = okResult.Value!;
        var propertyInfo = okResult.Value!.GetType().GetProperty("projectId");
        var actualProjectId = propertyInfo?.GetValue(okResult.Value);
        Assert.Equal(projectId, actualProjectId);
    }

    [Fact]
    public async Task GetProject_ReturnsNotFound_WhenProjectDoesNotExist()
    {
        // Arrange
        _mediatorMock.Setup(m => m.Send(It.IsAny<GetProjectByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProjectEntity)null!);

        // Act
        var result = await _controller.GetProject(Guid.NewGuid());

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task GetProject_ReturnsOk_WithProject_WhenProjectExists()
    {
        // Arrange
        var dummyAudio = new AudioFileEntity();
        var dummyProject = ProjectEntity.Create("es", dummyAudio);
        typeof(ProjectEntity).GetProperty("Id")?.SetValue(dummyProject, Guid.NewGuid());

        _mediatorMock.Setup(m => m.Send(It.IsAny<GetProjectByIdCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyProject);

        // Act
        var result = await _controller.GetProject(Guid.NewGuid());

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(dummyProject, okResult.Value);
    }
}