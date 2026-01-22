using System.Security.Claims;
using System.Text;
using Application.Interfaces;
using Core.Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace WebApi.Tests.Controllers;

public class FilesControllerTests
{
    private readonly FilesController _controller;
    private readonly Mock<IFileStorageService> _fileStorageServiceMock;
    private readonly Mock<IActivityPublisher> _activityPublisherMock;
    private readonly Mock<IDocumentRepository> _documentRepositoryMock;
    private readonly Mock<IKnowledgeBaseFileRepository> _kbRepositoryMock;
    private readonly Mock<IMediaCenterAssetRepository> _assetRepositoryMock;

    public FilesControllerTests()
    {
        _fileStorageServiceMock = new Mock<IFileStorageService>();
        _activityPublisherMock = new Mock<IActivityPublisher>();
        _documentRepositoryMock = new Mock<IDocumentRepository>();
        _kbRepositoryMock = new Mock<IKnowledgeBaseFileRepository>();
        _assetRepositoryMock = new Mock<IMediaCenterAssetRepository>();

        _controller = new FilesController(
            _fileStorageServiceMock.Object,
            _activityPublisherMock.Object,
            _documentRepositoryMock.Object,
            _kbRepositoryMock.Object,
            _assetRepositoryMock.Object);
    }

    #region Helper Methods

    private void SetupAuthenticatedUser(Guid userId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    private IFormFile CreateMockFile(string fileName, string contentType, long fileSize, byte[] content)
    {
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, fileSize, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
        return formFile;
    }

    #endregion

    #region Upload Tests - Validation

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenSessionIdIsMissing()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var file = CreateMockFile("test.pdf", "application/pdf", 1024, new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.Upload(file, null, null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("SessionId is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenSessionIdIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var file = CreateMockFile("test.pdf", "application/pdf", 1024, new byte[] { 0x25, 0x50, 0x44, 0x46 });

        // Act
        var result = await _controller.Upload(file, "", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("SessionId is required.", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileIsNull()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());

        // Act
        var result = await _controller.Upload(null, "session-123", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded.", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileIsEmpty()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var file = CreateMockFile("test.pdf", "application/pdf", 0, Array.Empty<byte>());

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("No file uploaded.", badRequestResult.Value);
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenContentTypeIsNotAllowed()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var file = CreateMockFile("test.exe", "application/x-msdownload", 1024, new byte[] { 0x4D, 0x5A });

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("not allowed", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenFileSizeExceedsLimit()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var largeContent = new byte[101 * 1024 * 1024]; // 101 MB
        var file = CreateMockFile("large.pdf", "application/pdf", largeContent.Length, largeContent);

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("exceeds the maximum allowed size", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenMagicBytesDoNotMatch()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        // PDF content type but PNG magic bytes
        var file = CreateMockFile("fake.pdf", "application/pdf", 8, 
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("does not match the declared content type", badRequestResult.Value?.ToString());
    }

    [Fact]
    public async Task Upload_ReturnsBadRequest_WhenDocumentTypeIsInvalid()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var file = CreateMockFile("test.pdf", "application/pdf", 4, new byte[] { 0x25, 0x50, 0x44, 0x46 });

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        // Act
        var result = await _controller.Upload(file, "session-123", "invalid-type");

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("Invalid document type.", badRequestResult.Value);
    }

    #endregion

    #region Upload Tests - Success Cases

    [Fact]
    public async Task Upload_ReturnsOk_WithFileMetadata_WhenPdfUploaded()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var pdfContent = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D }; // %PDF-
        var file = CreateMockFile("document.pdf", "application/pdf", pdfContent.Length, pdfContent);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = okResult.Value;
        Assert.NotNull(response);
        
        var contentTypeProperty = response!.GetType().GetProperty("contentType");
        Assert.NotNull(contentTypeProperty);
        Assert.Equal("application/pdf", contentTypeProperty!.GetValue(response));
        
        var sizeProperty = response.GetType().GetProperty("size");
        Assert.NotNull(sizeProperty);
        Assert.Equal((long)pdfContent.Length, sizeProperty!.GetValue(response));
    }

    [Fact]
    public async Task Upload_ReturnsOk_WhenImageUploaded()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var jpegContent = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var file = CreateMockFile("image.jpg", "image/jpeg", jpegContent.Length, jpegContent);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Upload(file, "session-123", null);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Upload_CallsFileStorageService_WithCorrectParameters()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var content = Encoding.UTF8.GetBytes("test content");
        var file = CreateMockFile("test.txt", "text/plain", content.Length, content);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Upload(file, "session-123", null);

        // Assert
        _fileStorageServiceMock.Verify(
            s => s.StoreFileAsync(
                It.IsAny<Stream>(),
                It.Is<string>(name => name.Contains("test") && name.EndsWith(".txt")),
                "text/plain"),
            Times.Once);
    }

    [Fact]
    public async Task Upload_PublishesActivities_ForFileReceiveAndSave()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var content = Encoding.UTF8.GetBytes("test");
        var file = CreateMockFile("test.txt", "text/plain", content.Length, content);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Upload(file, "session-123", null);

        // Assert
        _activityPublisherMock.Verify(
            a => a.PublishAsync("file_received", It.IsAny<object>()),
            Times.Once);
        _activityPublisherMock.Verify(
            a => a.PublishAsync("file_saved", It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task Upload_SavesDocumentToRepository_WithCorrectMetadata()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());
        var content = Encoding.UTF8.GetBytes("test");
        var file = CreateMockFile("test.txt", "text/plain", content.Length, content);
        var sessionId = "session-123";

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Upload(file, sessionId, null);

        // Assert
        _documentRepositoryMock.Verify(
            d => d.AddAsync(It.Is<DocumentEntity>(doc =>
                doc.SessionId == sessionId &&
                doc.ContentType == "text/plain" &&
                doc.FileSize == content.Length &&
                doc.Enum == DocumentEnum.Attachment)),
            Times.Once);
    }

    [Fact]
    public async Task Upload_CreatesKnowledgeBaseFile_WhenTypeIsKnowledge()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var content = Encoding.UTF8.GetBytes("test");
        var file = CreateMockFile("kb.txt", "text/plain", content.Length, content);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _kbRepositoryMock
            .Setup(k => k.AddAsync(It.IsAny<KnowledgeBaseFileEntity>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Upload(file, "session-123", "knowledgebase");

        // Assert
        _kbRepositoryMock.Verify(
            k => k.AddAsync(It.Is<KnowledgeBaseFileEntity>(kb =>
                kb.UserId == userId &&
                kb.MimeType == "text/plain"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Upload_CreatesMediaCenterAsset_WhenKnowledgeBaseFileIsAudio()
    {
        // Arrange
        var userId = Guid.NewGuid();
        SetupAuthenticatedUser(userId);
        var mp3Content = new byte[] { 0xFF, 0xFB, 0x90, 0x00 }; // MP3 magic bytes
        var file = CreateMockFile("audio.mp3", "audio/mpeg", mp3Content.Length, mp3Content);

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _kbRepositoryMock
            .Setup(k => k.AddAsync(It.IsAny<KnowledgeBaseFileEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _assetRepositoryMock
            .Setup(a => a.AddAsync(It.IsAny<MediaCenterAssetEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Upload(file, "session-123", "knowledgebase");

        // Assert
        _assetRepositoryMock.Verify(
            a => a.AddAsync(It.Is<MediaCenterAssetEntity>(asset =>
                asset.MimeType == "audio/mpeg")),
            Times.Once);
    }

    [Fact]
    public async Task Upload_AcceptsVariousDocumentFormats()
    {
        // Arrange
        SetupAuthenticatedUser(Guid.NewGuid());

        var testCases = new[]
        {
            ("test.json", "application/json", Encoding.UTF8.GetBytes("{}")),
            ("test.csv", "text/csv", Encoding.UTF8.GetBytes("col1,col2")),
            ("test.xml", "text/xml", Encoding.UTF8.GetBytes("<root/>"))
        };

        _fileStorageServiceMock
            .Setup(s => s.StoreFileAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync("stored-path");

        _activityPublisherMock
            .Setup(a => a.PublishAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        _documentRepositoryMock
            .Setup(d => d.AddAsync(It.IsAny<DocumentEntity>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        foreach (var (fileName, contentType, content) in testCases)
        {
            // Act
            var file = CreateMockFile(fileName, contentType, content.Length, content);
            var result = await _controller.Upload(file, "session-123", null);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }
    }

    [Fact]
    public async Task FilesController_HasAuthorizeAttribute()
    {
        // Arrange
        var controllerType = typeof(FilesController);

        // Assert
        var authorizeAttribute = controllerType.GetCustomAttributes(
            typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false);
        Assert.Single(authorizeAttribute);
    }

    #endregion
}
